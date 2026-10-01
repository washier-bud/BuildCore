using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class RebootOptimizationExperimentPreparationService
    {
        private readonly BenchmarkService _benchmarkService;

        public RebootOptimizationExperimentPreparationService(
            BenchmarkService benchmarkService)
        {
            _benchmarkService =
                benchmarkService ??
                throw new ArgumentNullException(
                    nameof(benchmarkService));
        }

        public async Task<RebootOptimizationExperimentState> PrepareAsync(
            OptimizationRecommendation recommendation,
            BenchmarkWorkload workload,
            CancellationToken cancellationToken = default)
        {
            if (recommendation == null)
                throw new ArgumentNullException(nameof(recommendation));

            if (!recommendation.CanTest ||
                !recommendation.RequiresReboot ||
                recommendation.TestType !=
                    OptimizationTestType.ExperimentalReboot)
            {
                throw new InvalidOperationException(
                    "This optimization is not configured for a reboot experiment.");
            }

            if (workload == null || !workload.IsValid)
            {
                throw new InvalidOperationException(
                    "The workload definition is invalid.");
            }

            if (RebootOptimizationExperimentStorageService.GetPending() != null)
            {
                throw new InvalidOperationException(
                    "A reboot optimization experiment is already pending.");
            }

            var experiment =
                new RebootOptimizationExperimentState
                {
                    OptimizationTitle =
                        recommendation.Title,

                    WorkloadDefinition =
                        workload,

                    WorkloadFingerprint =
                        WorkloadFingerprintService.Calculate(workload),

                    Phase =
                        RebootOptimizationExperimentPhase.NotStarted,

                    Status =
                        "Preparing baseline workload benchmark."
                };

            RebootOptimizationExperimentStorageService.Save(
                experiment);

            WorkloadBenchmarkResult baseline =
                await RunWorkloadAsync(
                    workload,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!baseline.IsComplete ||
                baseline.CompletedRuns < workload.RunCount)
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Inconclusive;

                experiment.Status =
                    "Baseline workload benchmark did not complete.";

                experiment.Baseline =
                    baseline;

                RebootOptimizationExperimentStorageService.Save(
                    experiment);

                return experiment;
            }

            experiment.Baseline =
                baseline;

            experiment.BaselineCompleted =
                true;

            experiment.BaselineEnvironment =
                baseline.Environment ??
                WorkloadEnvironmentSnapshot.Capture();

            experiment.Phase =
                RebootOptimizationExperimentPhase.BaselineCompleted;

            experiment.Status =
                "Baseline completed.";

            RebootOptimizationExperimentStorageService.Save(
                experiment);

            BuildCoreSnapshot snapshot =
                SnapshotService.CreateSnapshot();

            experiment.SnapshotId =
                snapshot.Id;

            experiment.SnapshotCreated =
                true;

            experiment.Phase =
                RebootOptimizationExperimentPhase.SnapshotCreated;

            experiment.Status =
                "Snapshot created.";

            RebootOptimizationExperimentStorageService.Save(
                experiment);

            if (!string.IsNullOrWhiteSpace(
                workload.TargetProcessPath))
            {
                experiment.ExpectedTargetProcessPath =
                    workload.TargetProcessPath;

                experiment.ExpectedTargetProcessStartTimeUtc =
                    workload.TargetProcessStartTimeUtc;
            }

            OptimizationApplyResult applyResult =
                OperatingSystem.IsWindows()
                    ? ApplyRebootOptimization(recommendation)
                    : new OptimizationApplyResult
                    {
                        Success = false,
                        Verified = false,
                        Message = "BuildCore reboot optimizations are supported only on Windows.",
                        Error = "Unsupported platform."
                    };

            if (!applyResult.Success)
            {
                experiment.Phase =
                    RebootOptimizationExperimentPhase.Failed;

                experiment.Status =
                    applyResult.Message;

                RebootOptimizationExperimentStorageService.Save(
                    experiment);

                throw new InvalidOperationException(
                    applyResult.Message);
            }

            experiment.OptimizationChangePending =
                true;

            experiment.RebootRequired =
                true;

            experiment.Phase =
                RebootOptimizationExperimentPhase.OptimizationPendingReboot;

            experiment.Status =
                "Optimization change applied. Windows restart required.";

            RebootOptimizationExperimentStorageService.Save(
                experiment);

            return experiment;
        }

        private async Task<WorkloadBenchmarkResult> RunWorkloadAsync(
            BenchmarkWorkload workload,
            CancellationToken cancellationToken)
        {
            IBenchmarkWorkload provider;

            if (workload.RequiresInteractiveWorkload)
            {
                var frameTimeSource =
                    new PresentMonFrameTimeSource();

                provider =
                    new PresentMonWorkload(
                        _benchmarkService,
                        frameTimeSource,
                        workload);
            }
            else
            {
                provider =
                    new TelemetryWorkload(
                        _benchmarkService,
                        workload);
            }

            var service =
                new WorkloadBenchmarkService(
                    provider);

            return await service.RunAsync(cancellationToken);
        }

        private static OptimizationApplyResult
            ApplyRebootOptimization(
                OptimizationRecommendation recommendation)
        {
            if (recommendation.Title ==
                "Hardware-Accelerated GPU Scheduling")
            {
                using RegistryKey? key =
                    Registry.LocalMachine.CreateSubKey(
                        @"SYSTEMCurrentControlSetControlGraphicsDrivers");

                if (key == null)
                {
                    return new OptimizationApplyResult
                    {
                        Success = false,
                        Verified = false,
                        Message =
                            "BuildCore could not access the HAGS registry setting.",
                        Error =
                            "Registry access failed."
                    };
                }

                key.SetValue(
                    "HwSchMode",
                    2,
                    RegistryValueKind.DWord);

                key.Flush();

                return new OptimizationApplyResult
                {
                    Success = true,
                    Verified = true,
                    Message =
                        "Hardware-Accelerated GPU Scheduling was enabled. " +
                        "Windows restart is required before benchmarking."
                };
            }

            return new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message =
                    "No reboot-optimization apply handler exists for this optimization.",
                Error =
                    recommendation.Title
            };
        }
    }
}
