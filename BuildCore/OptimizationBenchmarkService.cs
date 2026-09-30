using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BuildCore
{
    public class OptimizationBenchmarkService
    {
        private readonly ReliableBenchmarkService _reliableBenchmarkService;

        public OptimizationBenchmarkService(
            ReliableBenchmarkService reliableBenchmarkService)
        {
            _reliableBenchmarkService =
                reliableBenchmarkService
                ?? throw new ArgumentNullException(
                    nameof(reliableBenchmarkService));
        }

        public async Task<OptimizationBenchmarkResult>
            RunAsync(
                OptimizationRecommendation recommendation,
                int runCount = 3,
                int benchmarkDurationSeconds = 5,
                int sampleIntervalMilliseconds = 100,
                int settleDelayMilliseconds = 1500)
        {
            if (recommendation == null)
                throw new ArgumentNullException(
                    nameof(recommendation));

            if (!recommendation.CanApply)
                throw new InvalidOperationException(
                    "This optimization cannot be applied automatically.");

            if (runCount <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(runCount));

            if (benchmarkDurationSeconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(benchmarkDurationSeconds));

            if (sampleIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sampleIntervalMilliseconds));

            if (settleDelayMilliseconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(settleDelayMilliseconds));

            var result =
                new OptimizationBenchmarkResult
                {
                    TestId =
                        Guid.NewGuid().ToString("N"),

                    StartedAt =
                        DateTime.Now,

                    Status =
                        "Starting",

                    OptimizationTitle =
                        recommendation.Title,

                    BaselineCompleted =
                        false,

                    SnapshotCreated =
                        false,

                    OptimizationApplied =
                        false,

                    OptimizationVerified =
                        false,

                    AfterBenchmarkCompleted =
                        false,

                    ComparisonCompleted =
                        false,

                    AnalysisCompleted =
                        false
                };

            try
            {
                // STEP 1 — RELIABLE BASELINE

                result.Status =
                    "Running reliable baseline";

                ReliableBenchmarkResult baseline =
                    await _reliableBenchmarkService.RunAsync(
                        runCount,
                        benchmarkDurationSeconds,
                        sampleIntervalMilliseconds);

                if (!baseline.IsComplete)
                {
                    result.Status =
                        "Baseline benchmark failed";

                    return CompleteAndSave(result);
                }

                result.Baseline =
                    ConvertReliableResultToBenchmarkResult(
                        baseline);

                result.BaselineCompleted =
                    true;

                // STEP 2 — SAFETY SNAPSHOT

                result.Status =
                    "Creating safety snapshot";

                BuildCoreSnapshot snapshot =
                    SnapshotService.CreateSnapshot();

                if (string.IsNullOrWhiteSpace(snapshot.Id))
                {
                    result.Status =
                        "Snapshot creation failed";

                    return CompleteAndSave(result);
                }

                if (!SnapshotService.ValidateSnapshot(
                    snapshot.Id))
                {
                    result.Status =
                        "Snapshot validation failed";

                    return CompleteAndSave(result);
                }

                result.SnapshotId =
                    snapshot.Id;

                result.SnapshotCreated =
                    true;

                // STEP 3 — TRANSACTION

                OptimizationTransaction transaction =
                    OptimizationTransactionService
                        .CreateTransaction(
                            snapshot.Id,
                            recommendation);

                // STEP 4 — APPLY

                result.Status =
                    "Applying optimization";

                OptimizationApplyResult applyResult =
                    OptimizationApplyService.Apply(
                        recommendation);

                result.ApplyResult =
                    applyResult;

                OptimizationTransactionService
                    .CompleteTransaction(
                        transaction,
                        applyResult);

                result.OptimizationApplied =
                    applyResult.Success;

                result.OptimizationVerified =
                    applyResult.Verified;

                if (!applyResult.Success ||
                    !applyResult.Verified)
                {
                    result.Status =
                        "Optimization failed verification";

                    return CompleteAndSave(result);
                }

                // STEP 5 — SETTLE

                result.Status =
                    "Allowing system to settle";

                if (settleDelayMilliseconds > 0)
                {
                    await Task.Delay(
                        settleDelayMilliseconds);
                }

                // STEP 6 — RELIABLE AFTER

                result.Status =
                    "Running reliable after benchmark";

                ReliableBenchmarkResult after =
                    await _reliableBenchmarkService.RunAsync(
                        runCount,
                        benchmarkDurationSeconds,
                        sampleIntervalMilliseconds);

                if (!after.IsComplete)
                {
                    result.Status =
                        "After benchmark failed";

                    return CompleteAndSave(result);
                }

                result.After =
                    ConvertReliableResultToBenchmarkResult(
                        after);

                result.AfterBenchmarkCompleted =
                    true;

                // STEP 7 — BENCHMARK VALIDATION

                BenchmarkResult? baselineBenchmark =
                    result.Baseline;

                BenchmarkResult? afterBenchmark =
                    result.After;

                if (baselineBenchmark == null ||
                    afterBenchmark == null)
                {
                    result.Status =
                        "Benchmark conversion failed";

                    return CompleteAndSave(result);
                }

                // STEP 8 — LEGACY TELEMETRY COMPARISON

                result.Status =
                    "Creating telemetry comparison";

                result.Comparison =
                    BenchmarkComparisonService.Compare(
                        recommendation.Title,
                        baselineBenchmark,
                        afterBenchmark);

                result.ComparisonCompleted =
                    result.Comparison != null;

                if (!result.ComparisonCompleted)
                {
                    result.Status =
                        "Comparison creation failed";

                    return CompleteAndSave(result);
                }

                // STEP 9 — RELIABLE STATISTICAL ANALYSIS

                result.Status =
                    "Analyzing benchmark consistency";

                OptimizationTestAnalysis analysis =
                    OptimizationTestAnalysis.Analyze(
                        baseline,
                        after);

                result.Analysis =
                    analysis;

                result.AnalysisCompleted =
                    true;

                // STEP 10 — COMPLETE

                result.Status =
                    "Completed";

                return CompleteAndSave(result);
            }
            catch (Exception ex)
            {
                result.Status =
                    "Failed";

                Debug.WriteLine(
                    "BUILDCORE CONTROLLED TEST ERROR");

                Debug.WriteLine(
                    ex.ToString());

                return CompleteAndSave(result);
            }
        }

        private static BenchmarkResult
            ConvertReliableResultToBenchmarkResult(
                ReliableBenchmarkResult reliable)
        {
            var result =
                new BenchmarkResult
                {
                    BenchmarkId =
                        Guid.NewGuid().ToString("N"),

                    StartedAt =
                        reliable.StartedAt,

                    CompletedAt =
                        reliable.CompletedAt,

                    Status =
                        reliable.IsComplete
                            ? "Completed"
                            : "Incomplete",

                    SampleCount =
                        CalculateTotalSamples(
                            reliable),

                    CpuAverageUsage =
                        reliable.CpuAverage,

                    CpuPeakUsage =
                        reliable.CpuPeakAverage,

                    RamAverageUsage =
                        reliable.RamAverage,

                    RamPeakUsage =
                        reliable.RamPeakAverage,

                    DiskAverageActivity =
                        reliable.DiskAverage,

                    DiskPeakActivity =
                        reliable.DiskPeakAverage,

                    GpuAverageUsage =
                        reliable.GpuAverage,

                    GpuPeakUsage =
                        reliable.GpuPeakAverage,

                    GpuAverageClockMHz =
                        reliable.GpuClockAverageMHz,

                    GpuAverageTemperature =
                        reliable.GpuTemperatureAverage,

                    GpuPeakTemperature =
                        reliable.GpuTemperaturePeakAverage,

                    GpuAverageMemoryUsedGB =
                        reliable.VramAverageGB,

                    GpuPeakMemoryUsedGB =
                        reliable.VramPeakAverageGB,

                    Duration =
                        TimeSpan.FromSeconds(
                            reliable.DurationAverageSeconds),

                    PerformanceScore =
                        0
                };

            result.Summary =
                $"Reliable benchmark completed using " +
                $"{reliable.CompletedRuns} runs.";

            return result;
        }

        private static int CalculateTotalSamples(
            ReliableBenchmarkResult reliable)
        {
            int total = 0;

            foreach (BenchmarkRun run in reliable.Runs)
            {
                total +=
                    Math.Max(
                        0,
                        run.SampleCount);
            }

            return total;
        }

        private OptimizationBenchmarkResult
            CompleteAndSave(
                OptimizationBenchmarkResult result)
        {
            result.CompletedAt =
                DateTime.Now;

            try
            {
                OptimizationTestStorageService
                    .SaveTest(result);

                Debug.WriteLine(
                    "--------------------------------");

                Debug.WriteLine(
                    "OPTIMIZATION TEST SAVED");

                Debug.WriteLine(
                    $"Test ID: {result.TestId}");

                Debug.WriteLine(
                    $"Status: {result.Status}");

                Debug.WriteLine(
                    $"Path: " +
                    $"{OptimizationTestStorageService.GetTestDirectory()}");

                Debug.WriteLine(
                    "--------------------------------");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE TEST SAVE ERROR");

                Debug.WriteLine(
                    ex.ToString());
            }

            return result;
        }
    }
}