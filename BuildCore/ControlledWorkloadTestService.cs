using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class ControlledWorkloadTestService
    {
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
                OptimizationTitle = recommendation.Title,
                SnapshotId = "",
                WorkloadDefinition = workload,
                WorkloadBaselineCompleted = false,
                WorkloadSnapshotCreated = false,
                WorkloadOptimizationApplied = false,
                WorkloadOptimizationVerified = false,
                WorkloadAfterCompleted = false,
                WorkloadAnalysisCompleted = false
            };

            try
            {
                // 1. BASELINE — use the exact same workload definition.
                result.Status = "Running workload baseline";

                IBenchmarkWorkload baselineWorkload =
                    CreateWorkload(workload);

                var baselineService =
                    new WorkloadBenchmarkService(baselineWorkload);

                WorkloadBenchmarkResult baseline =
                    await baselineService.RunAsync(cancellationToken);

                result.WorkloadBaseline = baseline;

                if (!baseline.IsComplete || !baseline.HasEnoughRuns)
                {
                    result.Status =
                        $"Workload baseline incomplete: " +
                        $"{baseline.CompletedRuns}/{baseline.RequestedRuns} runs.";
                    return CompleteAndSave(result);
                }

                result.WorkloadBaselineCompleted = true;

                // 2. SAFETY SNAPSHOT
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Creating safety snapshot";

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

                // 3. TRANSACTION
                OptimizationTransaction transaction =
                    OptimizationTransactionService.CreateTransaction(
                        snapshot.Id,
                        recommendation);

                // 4. APPLY + VERIFY
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Applying optimization";

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

                    await Task.Delay(
                        settleDelayMilliseconds,
                        cancellationToken);
                }

                // 6. AFTER — same workload ID and target process.
                cancellationToken.ThrowIfCancellationRequested();

                result.Status = "Running workload after benchmark";

                if (workload.TargetProcessId > 0 &&
                    !IsProcessRunning(workload.TargetProcessId))
                {
                    result.Status =
                        "Target workload process is no longer running.";
                    return CompleteAndSave(result);
                }

                IBenchmarkWorkload afterWorkload =
                    CreateWorkload(workload);

                var afterService =
                    new WorkloadBenchmarkService(afterWorkload);

                WorkloadBenchmarkResult after =
                    await afterService.RunAsync(cancellationToken);

                result.WorkloadAfter = after;

                if (!after.IsComplete || !after.HasEnoughRuns)
                {
                    result.Status =
                        $"Workload after benchmark incomplete: " +
                        $"{after.CompletedRuns}/{after.RequestedRuns} runs.";
                    return CompleteAndSave(result);
                }

                result.WorkloadAfterCompleted = true;

                // 7. STATISTICAL COMPARISON
                result.Status = "Analyzing workload performance";

                result.WorkloadAnalysis =
                    WorkloadStatisticalAnalysis.Analyze(
                        baseline,
                        after);

                result.WorkloadAnalysisCompleted =
                    result.WorkloadAnalysis.IsComparable;

                if (!result.WorkloadAnalysisCompleted)
                {
                    result.Status =
                        "Workload comparison was inconclusive.";
                    return CompleteAndSave(result);
                }

                result.Status = "Completed";

                return CompleteAndSave(result);
            }
            catch (OperationCanceledException)
            {
                result.Status = "Canceled";
                return CompleteAndSave(result);
            }
            catch (Exception ex)
            {
                result.Status = "Failed";

                Debug.WriteLine(
                    "BUILDCORE CONTROLLED WORKLOAD TEST ERROR");
                Debug.WriteLine(ex.ToString());

                return CompleteAndSave(result);
            }
        }

        private static IBenchmarkWorkload CreateWorkload(
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
                    new BenchmarkService(),
                    frameTimeSource,
                    definition);
            }

            return new TelemetryWorkload(
                new BenchmarkService(),
                definition);
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
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
