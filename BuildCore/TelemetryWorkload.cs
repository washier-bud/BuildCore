using System;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class TelemetryWorkload : IBenchmarkWorkload
    {
        private readonly BenchmarkService _benchmarkService;

        public BenchmarkWorkload Definition { get; }

        public TelemetryWorkload(
            BenchmarkService benchmarkService,
            BenchmarkWorkload definition)
        {
            _benchmarkService =
                benchmarkService
                ?? throw new ArgumentNullException(nameof(benchmarkService));

            Definition =
                definition
                ?? throw new ArgumentNullException(nameof(definition));

            if (!Definition.IsValid)
                throw new ArgumentException(
                    "The telemetry workload definition is invalid.",
                    nameof(definition));
        }

        public async Task<WorkloadRunResult> RunAsync(
            CancellationToken cancellationToken = default)
        {
            DateTime startedAt = DateTime.Now;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                BenchmarkResult benchmark =
                    await _benchmarkService.RunAsync(
                        Definition.DurationSeconds,
                        Definition.SampleIntervalMilliseconds);

                cancellationToken.ThrowIfCancellationRequested();

                var run = new BenchmarkRun
                {
                    RunId = benchmark.BenchmarkId,
                    StartedAt = benchmark.StartedAt,
                    CompletedAt = benchmark.CompletedAt,
                    Status = benchmark.Status,
                    CpuAverageUsage = benchmark.CpuAverageUsage,
                    CpuPeakUsage = benchmark.CpuPeakUsage,
                    RamAverageUsage = benchmark.RamAverageUsage,
                    RamPeakUsage = benchmark.RamPeakUsage,
                    DiskAverageActivity = benchmark.DiskAverageActivity,
                    DiskPeakActivity = benchmark.DiskPeakActivity,
                    GpuAverageUsage = benchmark.GpuAverageUsage,
                    GpuPeakUsage = benchmark.GpuPeakUsage,
                    GpuAverageClockMHz = benchmark.GpuAverageClockMHz,
                    GpuAverageTemperature = benchmark.GpuAverageTemperature,
                    GpuPeakTemperature = benchmark.GpuPeakTemperature,
                    GpuAverageMemoryUsedGB =
                        benchmark.GpuAverageMemoryUsedGB,
                    GpuPeakMemoryUsedGB =
                        benchmark.GpuPeakMemoryUsedGB,
                    DurationSeconds = benchmark.DurationSeconds,
                    SampleCount = benchmark.SampleCount,
                    FrameTime = null
                };

                return new WorkloadRunResult
                {
                    RunId = run.RunId,
                    WorkloadId = Definition.WorkloadId,
                    WorkloadName = Definition.Name,
                    WorkloadType = Definition.Type,
                    BenchmarkRun = run,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.Now,
                    Status = run.IsSuccessful
                        ? "Completed"
                        : "Failed",
                    Summary =
                        "Telemetry-only workload completed. " +
                        "No real frame-time source was used."
                };
            }
            catch (OperationCanceledException)
            {
                return new WorkloadRunResult
                {
                    WorkloadId = Definition.WorkloadId,
                    WorkloadName = Definition.Name,
                    WorkloadType = Definition.Type,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.Now,
                    Status = "Canceled",
                    Summary = "Telemetry workload was canceled."
                };
            }
            catch (Exception ex)
            {
                return new WorkloadRunResult
                {
                    WorkloadId = Definition.WorkloadId,
                    WorkloadName = Definition.Name,
                    WorkloadType = Definition.Type,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.Now,
                    Status = "Failed",
                    Summary = ex.Message
                };
            }
        }
    }
}
