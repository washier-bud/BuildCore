using System;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class PresentMonWorkload : IBenchmarkWorkload
    {
        private readonly BenchmarkService _benchmarkService;
        private readonly IFrameTimeSource _frameTimeSource;

        public BenchmarkWorkload Definition { get; }

        public PresentMonWorkload(
            BenchmarkService benchmarkService,
            IFrameTimeSource frameTimeSource,
            BenchmarkWorkload definition)
        {
            _benchmarkService =
                benchmarkService
                ?? throw new ArgumentNullException(nameof(benchmarkService));

            _frameTimeSource =
                frameTimeSource
                ?? throw new ArgumentNullException(nameof(frameTimeSource));

            Definition =
                definition
                ?? throw new ArgumentNullException(nameof(definition));

            if (!Definition.IsValid)
                throw new ArgumentException(
                    "The workload definition is invalid.",
                    nameof(definition));

            if (Definition.TargetProcessId <= 0)
                throw new ArgumentException(
                    "A target process ID is required for a PresentMon workload.",
                    nameof(definition));
        }

        public async Task<WorkloadRunResult> RunAsync(
            CancellationToken cancellationToken = default)
        {
            DateTime startedAt = DateTime.Now;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Task<BenchmarkResult> telemetryTask =
                    _benchmarkService.RunAsync(
                        Definition.DurationSeconds,
                        Definition.SampleIntervalMilliseconds);

                Task<FrameTimeCaptureResult> frameTimeTask =
                    _frameTimeSource.CaptureAsync(
                        Definition.TargetProcessId,
                        Definition.DurationSeconds,
                        cancellationToken);

                await Task.WhenAll(telemetryTask, frameTimeTask);

                cancellationToken.ThrowIfCancellationRequested();

                BenchmarkResult benchmark = await telemetryTask;
                FrameTimeCaptureResult frameTime = await frameTimeTask;

                if (!benchmark.Status.Equals(
                        "Completed",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return CreateFailedResult(
                        startedAt,
                        "Telemetry benchmark did not complete successfully.");
                }

                if (!frameTime.IsSuccessful ||
                    frameTime.Statistics == null)
                {
                    return CreateFailedResult(
                        startedAt,
                        "PresentMon did not capture valid frame-time data. " +
                        frameTime.Summary);
                }

                var run = new BenchmarkRun
                {
                    RunId = benchmark.BenchmarkId,
                    StartedAt = benchmark.StartedAt,
                    CompletedAt = benchmark.CompletedAt,
                    Status = "Completed",
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
                    FrameTime = frameTime.Statistics,
                    DurationSeconds = benchmark.DurationSeconds,
                    SampleCount = benchmark.SampleCount
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
                    Status = "Completed",
                    Summary =
                        $"Captured telemetry plus {frameTime.Samples.Count} " +
                        "real frame-time samples using PresentMon."
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
                    Summary = "PresentMon workload was canceled."
                };
            }
            catch (Exception ex)
            {
                return CreateFailedResult(startedAt, ex.Message);
            }
        }

        private WorkloadRunResult CreateFailedResult(
            DateTime startedAt,
            string summary)
        {
            return new WorkloadRunResult
            {
                WorkloadId = Definition.WorkloadId,
                WorkloadName = Definition.Name,
                WorkloadType = Definition.Type,
                StartedAt = startedAt,
                CompletedAt = DateTime.Now,
                Status = "Failed",
                Summary = summary
            };
        }
    }
}
