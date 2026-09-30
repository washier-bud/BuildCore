using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BuildCore
{
    public class ReliableBenchmarkService
    {
        private readonly BenchmarkService _benchmarkService;

        public ReliableBenchmarkService(
            BenchmarkService benchmarkService)
        {
            _benchmarkService =
                benchmarkService
                ?? throw new ArgumentNullException(
                    nameof(benchmarkService));
        }

        public async Task<ReliableBenchmarkResult> RunAsync(
            int runCount = 3,
            int benchmarkDurationSeconds = 5,
            int sampleIntervalMilliseconds = 100,
            int delayBetweenRunsMilliseconds = 500)
        {
            if (runCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runCount));
            }

            if (benchmarkDurationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(benchmarkDurationSeconds));
            }

            if (sampleIntervalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleIntervalMilliseconds));
            }

            if (delayBetweenRunsMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delayBetweenRunsMilliseconds));
            }

            var result =
                new ReliableBenchmarkResult
                {
                    ResultId =
                        Guid.NewGuid().ToString("N"),

                    StartedAt =
                        DateTime.Now,

                    Status =
                        "Running",

                    Phase =
                        "Preparing",

                    RequestedRuns =
                        runCount,

                    CompletedRuns =
                        0
                };

            Debug.WriteLine(
                "================================");

            Debug.WriteLine(
                "BUILDCORE RELIABLE BENCHMARK");

            Debug.WriteLine(
                "================================");

            Debug.WriteLine(
                $"Requested runs: {runCount}");

            Debug.WriteLine(
                $"Duration per run: " +
                $"{benchmarkDurationSeconds}s");

            try
            {
                for (int runNumber = 1;
                     runNumber <= runCount;
                     runNumber++)
                {
                    result.Phase =
                        $"Running benchmark {runNumber} of {runCount}";

                    Debug.WriteLine(
                        $"RUN {runNumber}/{runCount}");

                    BenchmarkResult benchmark =
                        await _benchmarkService.RunAsync(
                            benchmarkDurationSeconds,
                            sampleIntervalMilliseconds);

                    if (benchmark == null)
                    {
                        result.Status =
                            $"Run {runNumber} failed";

                        break;
                    }

                    if (!benchmark.Status.Equals(
                        "Completed",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        result.Status =
                            $"Run {runNumber} did not complete";

                        break;
                    }

                    if (benchmark.SampleCount <= 0)
                    {
                        result.Status =
                            $"Run {runNumber} produced no samples";

                        break;
                    }

                    var run =
                        ConvertToBenchmarkRun(
                            benchmark,
                            runNumber);

                    result.Runs.Add(run);

                    result.CompletedRuns =
                        result.Runs.Count;

                    Debug.WriteLine(
                        $"Run {runNumber} completed.");

                    Debug.WriteLine(
                        $"CPU: " +
                        $"{run.CpuAverageUsage:F2}%");

                    Debug.WriteLine(
                        $"GPU: " +
                        $"{run.GpuAverageUsage:F2}%");

                    Debug.WriteLine(
                        $"GPU Temp: " +
                        $"{run.GpuAverageTemperature:F2} C");

                    Debug.WriteLine(
                        $"VRAM: " +
                        $"{run.GpuAverageMemoryUsedGB:F2} GB");

                    if (runNumber < runCount &&
                        delayBetweenRunsMilliseconds > 0)
                    {
                        result.Phase =
                            "Allowing system to settle";

                        await Task.Delay(
                            delayBetweenRunsMilliseconds);
                    }
                }

                result.CompletedAt =
                    DateTime.Now;

                if (result.CompletedRuns >= runCount &&
                    result.Runs.Count >= runCount)
                {
                    result.Status =
                        "Completed";

                    result.Phase =
                        "Complete";
                }
                else
                {
                    result.Status =
                        "Incomplete";

                    result.Phase =
                        "Incomplete";
                }

                Debug.WriteLine(
                    "================================");

                Debug.WriteLine(
                    "RELIABLE BENCHMARK COMPLETE");

                Debug.WriteLine(
                    $"Completed runs: " +
                    $"{result.CompletedRuns}/{result.RequestedRuns}");

                Debug.WriteLine(
                    $"Reliability: " +
                    $"{result.ReliabilityStatus}");

                Debug.WriteLine(
                    "================================");

                return result;
            }
            catch (Exception ex)
            {
                result.CompletedAt =
                    DateTime.Now;

                result.Status =
                    "Failed";

                result.Phase =
                    "Failed";

                Debug.WriteLine(
                    "BUILDCORE RELIABLE BENCHMARK ERROR");

                Debug.WriteLine(
                    ex.ToString());

                return result;
            }
        }

        private static BenchmarkRun ConvertToBenchmarkRun(
            BenchmarkResult benchmark,
            int runNumber)
        {
            return new BenchmarkRun
            {
                RunId =
                    Guid.NewGuid().ToString("N"),

                RunNumber =
                    runNumber,

                StartedAt =
                    benchmark.StartedAt,

                CompletedAt =
                    benchmark.CompletedAt,

                Status =
                    benchmark.Status,

                CpuAverageUsage =
                    benchmark.CpuAverageUsage,

                CpuPeakUsage =
                    benchmark.CpuPeakUsage,

                RamAverageUsage =
                    benchmark.RamAverageUsage,

                RamPeakUsage =
                    benchmark.RamPeakUsage,

                DiskAverageActivity =
                    benchmark.DiskAverageActivity,

                DiskPeakActivity =
                    benchmark.DiskPeakActivity,

                GpuAverageUsage =
                    benchmark.GpuAverageUsage,

                GpuPeakUsage =
                    benchmark.GpuPeakUsage,

                GpuAverageClockMHz =
                    benchmark.GpuAverageClockMHz,

                GpuAverageTemperature =
                    benchmark.GpuAverageTemperature,

                GpuPeakTemperature =
                    benchmark.GpuPeakTemperature,

                GpuAverageMemoryUsedGB =
                    benchmark.GpuAverageMemoryUsedGB,

                GpuPeakMemoryUsedGB =
                    benchmark.GpuPeakMemoryUsedGB,

                DurationSeconds =
                    benchmark.DurationSeconds,

                SampleCount =
                    benchmark.SampleCount
            };
        }
    }
}