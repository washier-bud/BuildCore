using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BuildCore
{
    public class ControlledWorkloadTestService
    {
        private readonly BenchmarkService _benchmarkService;

        public ControlledWorkloadTestService(BenchmarkService benchmarkService)
        {
            _benchmarkService = benchmarkService
                ?? throw new ArgumentNullException(nameof(benchmarkService));
        }

        public async Task<OptimizationBenchmarkResult> RunAsync(
            OptimizationRecommendation recommendation,
            BenchmarkWorkload workload,
            int settleDelayMilliseconds = 1500,
            CancellationToken cancellationToken = default)
        {
            if (recommendation == null)
                throw new ArgumentNullException(nameof(recommendation));

            if (workload == null)
                throw new ArgumentNullException(nameof(workload));

            if (!recommendation.CanApply)
                throw new InvalidOperationException(
                    "This optimization cannot be applied automatically.");

            if (!recommendation.CanTest)
                throw new InvalidOperationException(
                    "This optimization is not enabled for controlled testing.");

            if (!workload.IsValid)
                throw new ArgumentException(
                    "The benchmark workload definition is invalid.",
                    nameof(workload));

            if (settleDelayMilliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(settleDelayMilliseconds));

            var result = new OptimizationBenchmarkResult
            {
                TestId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTime.Now,
                Status = "Starting workload test",
                TestState = ControlledWorkloadTestState.Preparing,
                OptimizationTitle = recommendation.Title,
                SnapshotId = "",
                WorkloadDefinition = workload,
                WorkloadFingerprint = WorkloadFingerprintService.Calculate(workload),
                WorkloadBaselineCompleted = false,
                WorkloadSnapshotCreated = false,
                WorkloadOptimizationApplied = false,
                WorkloadOptimizationVerified = false,
                WorkloadAfterCompleted = false,
                WorkloadAnalysisCompleted = false
            };

            try
            {
                if (workload.RequiresInteractiveWorkload &&
                    !ProcessIdentityService.Matches(
                        workload.TargetProcessId,
                        workload.TargetProcessStartTimeUtc,
                        workload.TargetProcessPath))
                {
                    result.Status =
                        "Target workload process identity could not be verified.";
                    result.TestState = ControlledWorkloadTestState.Inconclusive;
                    return CompleteAndSave(result);
                }

                // 1. BASELINE — use the exact same workload definition.
                result.Status = "Running workload baseline";
                result.TestState = ControlledWorkloadTestState.BaselineRunning;

                IBenchmarkWorkload baselineWorkload =
                    CreateWorkload(workload);

                var baselineService =
                    new WorkloadBenchmarkService(baselineWorkload);

                WorkloadBenchmarkResult baseline =
                    await baselineService.RunAsync(cancellationToken);

                result.WorkloadBaseline = baseline;
                result.WorkloadBaselineEnvironment = baseline.Environment;

                if (!ValidateWorkloadResult(
                        baseline,
                        workload,
                        out string baselineValidationError))
                {
                    result.Status =
                        $"Workload baseline validation failed: {baselineValidationError}";
                    return CompleteAndSave(result);
                }

                if (!baseline.IsComplete || !baseline.HasEnoughRuns)
                {
                    result.TestState = ControlledWorkloadTestState.Inconclusive;
                    result.Status =
                        $"Workload baseline incomplete: "
                        $"{baseline.CompletedRuns}/{baseline.RequestedRuns} runs.";
                    return CompleteAndSave(result);
                }

                result.WorkloadBaselineCompleted = true;
                result.TestState = ControlledWorkloadTestState.BaselineValidated;

                // 2. SAFETY SNAPSHOT
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Creating safety snapshot";
                result.TestState = ControlledWorkloadTestState.Preparing;

                BuildCoreSnapshot snapshot =
                    SnapshotService.CreateSnapshot();

                if (string.IsNullOrWhiteSpace(snapshot.Id) ||
                    !SnapshotService.ValidateSnapshot(snapshot.Id))
                {
                    result.Status = "Snapshot validation failed";
                    return CompleteAndSave(result);
                }

                result.SnapshotId = snapshot.Id;
                result.WorkloadSnapshotCreated = true;
                result.TestState = ControlledWorkloadTestState.SnapshotCreated;

                // 3. TRANSACTION
                OptimizationTransaction transaction =
                    OptimizationTransactionService.CreateTransaction(
                        snapshot.Id,
                        recommendation);

                // 4. APPLY + VERIFY
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Applying optimization";
                result.TestState = ControlledWorkloadTestState.OptimizationApplying;

                OptimizationApplyResult applyResult =
                    OptimizationApplyService.Apply(recommendation);

                result.ApplyResult = applyResult;

                OptimizationTransactionService.CompleteTransaction(
                    transaction,
                    applyResult);

                result.WorkloadOptimizationApplied =
                    applyResult.Success;

                result.WorkloadOptimizationVerified =
                    applyResult.Verified;

                if (applyResult.Success && applyResult.Verified)
                    result.TestState = ControlledWorkloadTestState.OptimizationVerified;

                if (!applyResult.Success || !applyResult.Verified)
                {
                    result.Status =
                        "Optimization failed verification";
                    return CompleteAndSave(result);
                }

                // 5. SETTLE
                if (settleDelayMilliseconds > 0)
                {
                    result.Status = "Allowing system to settle";
                    result.TestState = ControlledWorkloadTestState.Settling;

                    await Task.Delay(
                        settleDelayMilliseconds,
                        cancellationToken);
                }

                // 6. AFTER — same workload ID and target process.
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Running workload after benchmark";
                result.TestState = ControlledWorkloadTestState.AfterRunning;

                if (workload.RequiresInteractiveWorkload &&
                    !ProcessIdentityService.Matches(
                        workload.TargetProcessId,
                        workload.TargetProcessStartTimeUtc,
                        workload.TargetProcessPath))
                {
                    result.Status =
                        "Target workload process changed or ended before the after-test.";
                    result.TestState = ControlledWorkloadTestState.Inconclusive;
                    return CompleteAndSave(result);
                }

                IBenchmarkWorkload afterWorkload =
                    CreateWorkload(workload);

                var afterService =
                    new WorkloadBenchmarkService(afterWorkload);

                WorkloadBenchmarkResult after =
                    await afterService.RunAsync(cancellationToken);

                result.WorkloadAfter = after;
                result.WorkloadAfterEnvironment = after.Environment;

                if (!ValidateWorkloadResult(
                        after,
                        workload,
                        out string afterValidationError))
                {
                    result.Status =
                        $"Workload after validation failed: {afterValidationError}";
                    return CompleteAndSave(result);
                }

                if (!after.IsComplete || !after.HasEnoughRuns)
                {
                    result.TestState = ControlledWorkloadTestState.Inconclusive;
                    result.Status =
                        $"Workload after benchmark incomplete: "
                        $"{after.CompletedRuns}/{after.RequestedRuns} runs.";
                    return CompleteAndSave(result);
                }

                result.WorkloadAfterCompleted = true;
                result.TestState = ControlledWorkloadTestState.AfterValidated;

                // 7. STATISTICAL COMPARISON
                result.Status = "Analyzing workload performance";
                result.TestState = ControlledWorkloadTestState.AnalysisRunning;

                result.WorkloadAnalysis =
                    WorkloadStatisticalAnalysis.Analyze(
                        baseline,
                        after);

                result.WorkloadAnalysisCompleted =
                    result.WorkloadAnalysis.IsComparable;

                if (!result.WorkloadAnalysisCompleted)
                {
                    result.TestState = ControlledWorkloadTestState.Inconclusive;
                    result.Status =
                        "Workload comparison was inconclusive.";
                    return CompleteAndSave(result);
                }

                result.Status = "Completed";
                result.TestState = ControlledWorkloadTestState.Completed;

                return CompleteAndSave(result);
            }
            catch (OperationCanceledException)
            {
                result.Status = "Canceled";
                result.TestState = ControlledWorkloadTestState.Canceled;
                return CompleteAndSave(result);
            }
            catch (Exception ex)
            {
                result.Status = "Failed";
                result.TestState = ControlledWorkloadTestState.Failed;

                Debug.WriteLine(
                    "BUILDCORE CONTROLLED WORKLOAD TEST ERROR");
                Debug.WriteLine(ex.ToString());

                return CompleteAndSave(result);
            }
        }

        private IBenchmarkWorkload CreateWorkload(
            BenchmarkWorkload definition)
        {
            if (definition.RequiresInteractiveWorkload)
            {
                var frameTimeSource =
                    new PresentMonFrameTimeSource();

                if (!frameTimeSource.IsAvailable)
                {
                    throw new InvalidOperationException(
                        frameTimeSource.Description);
                }

                return new PresentMonWorkload(
                    _benchmarkService,
                    frameTimeSource,
                    definition);
            }

            return new TelemetryWorkload(
                _benchmarkService,
                definition);
        }

        private static bool ValidateWorkloadResult(
            WorkloadBenchmarkResult result,
            BenchmarkWorkload definition,
            out string error)
        {
            error = "";

            if (result == null)
            {
                error = "No result was returned.";
                return false;
            }

            if (!string.Equals(
                    result.WorkloadId,
                    definition.WorkloadId,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "Workload ID does not match the controlled test.";
                return false;
            }

            if (result.WorkloadType != definition.Type)
            {
                error = "Workload type changed between test phases.";
                return false;
            }

            if (result.RequestedRuns != definition.RunCount)
            {
                error = "Requested run count changed.";
                return false;
            }

            if (definition.RequiresInteractiveWorkload)
            {
                if (!ProcessIdentityService.Matches(
                        definition.TargetProcessId,
                        definition.TargetProcessStartTimeUtc,
                        definition.TargetProcessPath))
                {
                    error = "The target process identity is not valid.";
                    return false;
                }

                foreach (BenchmarkRun run in result.Runs)
                {
                    if (run.FrameTime == null ||
                        !run.FrameTime.HasData ||
                        run.FrameTime.SampleCount <= 0)
                    {
                        error = "A real frame-time capture is missing.";
                        return false;
                    }
                }
            }

            return true;
        }


        private static OptimizationBenchmarkResult CompleteAndSave(
            OptimizationBenchmarkResult result)
        {
            result.CompletedAt = DateTime.Now;

            try
            {
                OptimizationTestStorageService.SaveTest(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE CONTROLLED WORKLOAD SAVE ERROR");
                Debug.WriteLine(ex.ToString());
            }

            return result;
        }
    }
}
