using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class RebootOptimizationExperimentResumeService
    {
        private readonly BenchmarkService _benchmarkService;

        public RebootOptimizationExperimentResumeService(
            BenchmarkService benchmarkService)
        {
            _benchmarkService =
                benchmarkService ??
                throw new ArgumentNullException(
                    nameof(benchmarkService));
        }

        public async Task<RebootOptimizationExperimentState> ResumeAsync(
            RebootOptimizationExperimentState experiment,
            CancellationToken cancellationToken = default)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            if (!RebootOptimizationExperimentStorageService.Validate(
                experiment.ExperimentId))
            {
                throw new InvalidOperationException(
                    "The saved reboot experiment failed validation.");
            }

            if (!experiment.AfterRebootValidationPassed)
            {
                string validationMessage;

                if (!RebootOptimizationExperimentValidationService.ValidateAfterReboot(
                    experiment,
                    out validationMessage))
                {
                    experiment.Phase =
                        RebootOptimizationExperimentPhase.Inconclusive;

                    experiment.Status =
                        validationMessage;

                    RebootOptimizationExperimentStorageService.Save(experiment);

                    return experiment;
                }
            }

            experiment.Status =
                "Reboot validated. Preparing after-workload benchmark.";

            experiment.Phase =
                RebootOptimizationExperimentPhase.AfterBenchmarkPending;

            RebootOptimizationExperimentStorageService.Save(experiment);

            BenchmarkWorkload workload =
                experiment.WorkloadDefinition ??
                throw new InvalidOperationException(
                    "The saved workload definition is missing.");

            if (workload.RequiresInteractiveWorkload)
            {
                RunningProcessInfo? process =
                    RunningProcessService.GetRunningProcesses()
                        .FirstOrDefault(p =>
                            p.HasIdentity &&
                            string.Equals(
                                p.ExecutablePath,
                                workload.TargetProcessPath,
                                StringComparison.OrdinalIgnoreCase));

                if (process == null)
                {
                    experiment.Phase =
                        RebootOptimizationExperimentPhase.AfterBenchmarkPending;

                    experiment.Status =
                        "The target workload process is not running. " +
                        "Start the same application and resume the experiment again.";

                    RebootOptimizationExperimentStorageService.Save(experiment);

                    return experiment;
                }

                workload.TargetProcessId = process.ProcessId;
                workload.TargetProcessStartTimeUtc = process.StartTimeUtc;
                workload.TargetProcessPath = process.ExecutablePath;
            }

            string fingerprint =
                WorkloadFingerprintService.Calculate(workload);

            if (!string.Equals(
                fingerprint,
                experiment.WorkloadFingerprint,
                StringComparison.Ordinal))
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Inconclusive;

                experiment.Status =
                    "The workload configuration changed after reboot.";

                RebootOptimizationExperimentStorageService.Save(experiment);

                return experiment;
            }

            WorkloadBenchmarkResult after =
                await RunWorkloadAsync(
                    workload,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            experiment.AfterBenchmark =
                after;

            experiment.AfterEnvironment =
                after.Environment;

            experiment.AfterBenchmarkCompleted =
                after.IsComplete &&
                after.CompletedRuns >= workload.RunCount;

            if (!experiment.AfterBenchmarkCompleted)
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Inconclusive;

                experiment.Status =
                    "The after-workload benchmark did not complete.";

                RebootOptimizationExperimentStorageService.Save(experiment);

                return experiment;
            }

            WorkloadEnvironmentSnapshot beforeEnvironment =
                experiment.BaselineEnvironment ??
                experiment.Baseline?.Environment ??
                throw new InvalidOperationException(
                    "Baseline environment data is missing.");

            experiment.EnvironmentComparison =
                WorkloadEnvironmentComparison.Compare(
                    beforeEnvironment,
                    experiment.AfterEnvironment);

            if (!experiment.EnvironmentComparison.IsComparable)
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Inconclusive;

                experiment.Status =
                    experiment.EnvironmentComparison.Summary;

                RebootOptimizationExperimentStorageService.Save(experiment);

                return experiment;
            }

            experiment.Analysis =
                WorkloadStatisticalAnalysis.Analyze(
                    experiment.Baseline!,
                    after);

            experiment.AnalysisCompleted =
                experiment.Analysis.IsComparable;

            experiment.EvidenceQuality =
                BuildEvidenceQuality(experiment);

            experiment.EvidenceGatePassed =
                experiment.EvidenceQuality.IsSufficient;

            if (!experiment.AnalysisCompleted)
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Inconclusive;

                experiment.Status =
                    "The baseline and after-workload results are not comparable.";

                RebootOptimizationExperimentStorageService.Save(experiment);

                return experiment;
            }

            experiment.Phase =
                RebootOptimizationExperimentPhase.Completed;

            experiment.Status =
                "Reboot optimization experiment completed.";

            RebootOptimizationExperimentStorageService.Save(experiment);

            return experiment;
        }

        private async Task<WorkloadBenchmarkResult> RunWorkloadAsync(
            BenchmarkWorkload workload,
            CancellationToken cancellationToken)
        {
            IBenchmarkWorkload provider;

            if (workload.RequiresInteractiveWorkload)
            {
                provider =
                    new PresentMonWorkload(
                        _benchmarkService,
                        new PresentMonFrameTimeSource(),
                        workload);
            }
            else
            {
                provider =
                    new TelemetryWorkload(
                        _benchmarkService,
                        workload);
            }

            return await new WorkloadBenchmarkService(provider)
                .RunAsync(cancellationToken);
        }

        private static WorkloadEvidenceQuality BuildEvidenceQuality(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment.Baseline == null ||
                experiment.AfterBenchmark == null ||
                experiment.Analysis == null)
            {
                return new WorkloadEvidenceQuality
                {
                    AnalysisComplete = false,
                    Summary = "Evidence data is incomplete."
                };
            }

            var quality = new WorkloadEvidenceQuality
            {
                HasRealFrameTimeData =
                    experiment.Baseline.Runs.Any(r => r.FrameTime?.HasData == true) ||
                    experiment.AfterBenchmark.Runs.Any(r => r.FrameTime?.HasData == true),

                ConfigurationLocked =
                    string.Equals(
                        experiment.WorkloadFingerprint,
                        WorkloadFingerprintService.Calculate(
                            experiment.WorkloadDefinition!),
                        StringComparison.Ordinal),

                ProcessIdentityVerified =
                    !experiment.WorkloadDefinition!.RequiresInteractiveWorkload ||
                    experiment.AfterBenchmark.Runs.All(r =>
                        r.TargetProcessIdentity != null &&
                        r.TargetProcessIdentity.HasIdentity),

                PerRunConditionsCaptured =
                    experiment.Baseline.Runs.All(r => r.Environment != null) &&
                    experiment.AfterBenchmark.Runs.All(r => r.Environment != null),

                EnvironmentComparable =
                    experiment.EnvironmentComparison?.IsComparable == true,

                FingerprintsMatch =
                    string.Equals(
                        experiment.WorkloadFingerprint,
                        experiment.Baseline.WorkloadFingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        experiment.WorkloadFingerprint,
                        experiment.AfterBenchmark.WorkloadFingerprint,
                        StringComparison.Ordinal),

                AnalysisComplete =
                    experiment.Analysis.IsComparable,

                BaselineRunCount =
                    experiment.Baseline.CompletedRuns,

                AfterRunCount =
                    experiment.AfterBenchmark.CompletedRuns,

                PairedRunCount =
                    experiment.Analysis.PairedRunCount
            };

            quality.ConsistencyStatus =
                experiment.Analysis.AfterConsistency?.Status ??
                "Unknown";

            quality.EvidenceGrade =
                CalculateEvidenceGrade(
                    quality,
                    experiment.Analysis);

            quality.Summary =
                $"Evidence quality was evaluated after reboot validation and the after-workload benchmark. " +
                $"Evidence grade: {quality.EvidenceGrade}.";

            return quality;
        }

        private static string CalculateEvidenceGrade(
            WorkloadEvidenceQuality quality,
            WorkloadStatisticalAnalysis analysis)
        {
            int points = 0;

            if (quality.HasRealFrameTimeData) points += 25;
            if (quality.FingerprintsMatch) points += 15;
            if (quality.EnvironmentComparable) points += 10;
            if (quality.PerRunConditionsCaptured) points += 5;
            if (quality.ProcessIdentityVerified) points += 10;
            if (quality.BaselineRunCount >= 3) points += 10;
            if (quality.AfterRunCount >= 3) points += 10;
            if (quality.PairedRunCount >= 3) points += 10;
            if (quality.AnalysisComplete) points += 5;

            if (analysis.PairedDifferenceStandardDeviationPercent > 5)
                points -= 10;
            return points >= 90 ? "A" :
                   points >= 80 ? "B" :
                   points >= 70 ? "C" :
                   points >= 60 ? "D" : "F";
        }
    }
}
