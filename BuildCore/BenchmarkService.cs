using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class BenchmarkService
    {
        private readonly PerformanceService _performanceService;
        private readonly HardwareMonitorService _hardwareMonitorService;

        public BenchmarkService(
            PerformanceService performanceService,
            HardwareMonitorService hardwareMonitorService)
        {
            _performanceService =
                performanceService
                ?? throw new ArgumentNullException(
                    nameof(performanceService));

            _hardwareMonitorService =
                hardwareMonitorService
                ?? throw new ArgumentNullException(
                    nameof(hardwareMonitorService));
        }

        // ============================================================
        // ASYNC BENCHMARK
        // ============================================================

        public async Task<BenchmarkResult> RunAsync(
            int durationSeconds = 5,
            int sampleIntervalMilliseconds = 100)
        {
            if (durationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds));
            }

            if (sampleIntervalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleIntervalMilliseconds));
            }

            return await Task.Run(
                () =>
                    RunBenchmark(
                        durationSeconds,
                        sampleIntervalMilliseconds));
        }

        // ============================================================
        // BENCHMARK ENGINE
        // ============================================================

        private BenchmarkResult RunBenchmark(
            int durationSeconds,
            int sampleIntervalMilliseconds)
        {
            var result =
                new BenchmarkResult
                {
                    BenchmarkId =
                        Guid.NewGuid().ToString("N"),

                    StartedAt =
                        DateTime.Now,

                    Status =
                        "Running"
                };

            double cpuTotal = 0;
            double ramTotal = 0;
            double diskTotal = 0;

            double gpuTotal = 0;
            double gpuClockTotal = 0;
            double gpuTemperatureTotal = 0;
            double gpuMemoryTotal = 0;

            double peakCpu = 0;
            double peakRam = 0;
            double peakDisk = 0;
            double peakGpu = 0;
            double peakGpuTemperature = 0;
            double peakGpuMemory = 0;

            int gpuSamples = 0;
            int gpuClockSamples = 0;
            int gpuTemperatureSamples = 0;
            int gpuMemorySamples = 0;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            TimeSpan targetDuration =
                TimeSpan.FromSeconds(
                    durationSeconds);

            while (stopwatch.Elapsed < targetDuration)
            {
                PerformanceData performance =
                    _performanceService.GetPerformance();

                HardwareMonitorData hardware =
                    _hardwareMonitorService
                        .GetHardwareData();

                // ----------------------------------------------------
                // CPU
                // ----------------------------------------------------

                double cpu =
                    Math.Clamp(
                        performance.CpuUsage,
                        0,
                        100);

                cpuTotal += cpu;

                peakCpu =
                    Math.Max(
                        peakCpu,
                        cpu);

                // ----------------------------------------------------
                // RAM
                // ----------------------------------------------------

                double ram =
                    Math.Clamp(
                        performance.RamUsage,
                        0,
                        100);

                ramTotal += ram;

                peakRam =
                    Math.Max(
                        peakRam,
                        ram);

                // ----------------------------------------------------
                // DISK
                // ----------------------------------------------------

                double disk =
                    Math.Max(
                        0,
                        performance.DiskUsage);

                diskTotal += disk;

                peakDisk =
                    Math.Max(
                        peakDisk,
                        disk);

                // ----------------------------------------------------
                // GPU USAGE
                // ----------------------------------------------------

                if (hardware.GpuUsage.HasValue)
                {
                    double gpu =
                        Math.Clamp(
                            hardware.GpuUsage.Value,
                            0,
                            100);

                    gpuTotal += gpu;

                    peakGpu =
                        Math.Max(
                            peakGpu,
                            gpu);

                    gpuSamples++;
                }

                // ----------------------------------------------------
                // GPU CLOCK
                // ----------------------------------------------------

                if (hardware.GpuClock.HasValue &&
                    hardware.GpuClock.Value > 0)
                {
                    gpuClockTotal +=
                        hardware.GpuClock.Value;

                    gpuClockSamples++;
                }

                // ----------------------------------------------------
                // GPU TEMPERATURE
                // ----------------------------------------------------

                if (hardware.GpuTemperature.HasValue &&
                    hardware.GpuTemperature.Value > 0)
                {
                    double temperature =
                        hardware.GpuTemperature.Value;

                    gpuTemperatureTotal +=
                        temperature;

                    peakGpuTemperature =
                        Math.Max(
                            peakGpuTemperature,
                            temperature);

                    gpuTemperatureSamples++;
                }

                // ----------------------------------------------------
                // VRAM
                // ----------------------------------------------------

                if (hardware.GpuMemoryUsed.HasValue &&
                    hardware.GpuMemoryUsed.Value >= 0)
                {
                    double vramGB =
                        hardware.GpuMemoryUsed.Value /
                        1024.0;

                    gpuMemoryTotal +=
                        vramGB;

                    peakGpuMemory =
                        Math.Max(
                            peakGpuMemory,
                            vramGB);

                    gpuMemorySamples++;
                }

                result.SampleCount++;

                Thread.Sleep(
                    sampleIntervalMilliseconds);
            }

            stopwatch.Stop();

            result.CompletedAt =
                DateTime.Now;

            result.Duration =
                stopwatch.Elapsed;

            // ========================================================
            // AVERAGES
            // ========================================================

            if (result.SampleCount > 0)
            {
                result.CpuAverageUsage =
                    cpuTotal /
                    result.SampleCount;

                result.RamAverageUsage =
                    ramTotal /
                    result.SampleCount;

                result.DiskAverageActivity =
                    diskTotal /
                    result.SampleCount;
            }

            if (gpuSamples > 0)
            {
                result.GpuAverageUsage =
                    gpuTotal /
                    gpuSamples;
            }

            if (gpuClockSamples > 0)
            {
                result.CpuAverageClockMHz =
                    0;

                result.GpuAverageClockMHz =
                    gpuClockTotal /
                    gpuClockSamples;
            }

            if (gpuTemperatureSamples > 0)
            {
                result.GpuAverageTemperature =
                    gpuTemperatureTotal /
                    gpuTemperatureSamples;
            }

            if (gpuMemorySamples > 0)
            {
                result.GpuAverageMemoryUsedGB =
                    gpuMemoryTotal /
                    gpuMemorySamples;
            }

            // ========================================================
            // PEAKS
            // ========================================================

            result.CpuPeakUsage =
                peakCpu;

            result.RamPeakUsage =
                peakRam;

            result.DiskPeakActivity =
                peakDisk;

            result.GpuPeakUsage =
                peakGpu;

            result.GpuPeakTemperature =
                peakGpuTemperature;

            result.GpuPeakMemoryUsedGB =
                peakGpuMemory;

            // ========================================================
            // STATUS
            // ========================================================

            result.Status =
                "Completed";

            result.Summary =
                $"Baseline benchmark completed in " +
                $"{result.Duration.TotalSeconds:F2} seconds " +
                $"using {result.SampleCount} samples.";

            // ========================================================
            // IMPORTANT:
            // This is NOT a synthetic "FPS score".
            //
            // We leave PerformanceScore at 0 because this benchmark
            // measures telemetry only. A real BuildCore performance
            // score will be added later using controlled workloads.
            // ========================================================

            result.PerformanceScore =
                0;

            return result;
        }
    }
}