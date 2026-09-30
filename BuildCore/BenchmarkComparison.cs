using System;

namespace BuildCore
{
    public class BenchmarkComparison
    {
        // ============================================================
        // METADATA
        // ============================================================

        public string ComparisonId { get; set; } =
            Guid.NewGuid().ToString("N");

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;

        public string OptimizationTitle { get; set; } =
            "";

        public string BaselineBenchmarkId { get; set; } =
            "";

        public string AfterBenchmarkId { get; set; } =
            "";

        // ============================================================
        // CPU
        // ============================================================

        public double CpuAverageBefore { get; set; }

        public double CpuAverageAfter { get; set; }

        public double CpuAverageDelta
        {
            get
            {
                return
                    CpuAverageAfter -
                    CpuAverageBefore;
            }
        }

        public double CpuPeakBefore { get; set; }

        public double CpuPeakAfter { get; set; }

        public double CpuPeakDelta
        {
            get
            {
                return
                    CpuPeakAfter -
                    CpuPeakBefore;
            }
        }

        // ============================================================
        // RAM
        // ============================================================

        public double RamAverageBefore { get; set; }

        public double RamAverageAfter { get; set; }

        public double RamAverageDelta
        {
            get
            {
                return
                    RamAverageAfter -
                    RamAverageBefore;
            }
        }

        public double RamPeakBefore { get; set; }

        public double RamPeakAfter { get; set; }

        public double RamPeakDelta
        {
            get
            {
                return
                    RamPeakAfter -
                    RamPeakBefore;
            }
        }

        // ============================================================
        // DISK
        // ============================================================

        public double DiskAverageBefore { get; set; }

        public double DiskAverageAfter { get; set; }

        public double DiskAverageDelta
        {
            get
            {
                return
                    DiskAverageAfter -
                    DiskAverageBefore;
            }
        }

        public double DiskPeakBefore { get; set; }

        public double DiskPeakAfter { get; set; }

        public double DiskPeakDelta
        {
            get
            {
                return
                    DiskPeakAfter -
                    DiskPeakBefore;
            }
        }

        // ============================================================
        // GPU
        // ============================================================

        public double GpuAverageBefore { get; set; }

        public double GpuAverageAfter { get; set; }

        public double GpuAverageDelta
        {
            get
            {
                return
                    GpuAverageAfter -
                    GpuAverageBefore;
            }
        }

        public double GpuPeakBefore { get; set; }

        public double GpuPeakAfter { get; set; }

        public double GpuPeakDelta
        {
            get
            {
                return
                    GpuPeakAfter -
                    GpuPeakBefore;
            }
        }

        // ============================================================
        // GPU CLOCK
        // ============================================================

        public double GpuClockBeforeMHz { get; set; }

        public double GpuClockAfterMHz { get; set; }

        public double GpuClockDeltaMHz
        {
            get
            {
                return
                    GpuClockAfterMHz -
                    GpuClockBeforeMHz;
            }
        }

        // ============================================================
        // GPU TEMPERATURE
        // ============================================================

        public double GpuTemperatureBeforeC { get; set; }

        public double GpuTemperatureAfterC { get; set; }

        public double GpuTemperatureDeltaC
        {
            get
            {
                return
                    GpuTemperatureAfterC -
                    GpuTemperatureBeforeC;
            }
        }

        // ============================================================
        // VRAM
        // ============================================================

        public double VramAverageBeforeGB { get; set; }

        public double VramAverageAfterGB { get; set; }

        public double VramAverageDeltaGB
        {
            get
            {
                return
                    VramAverageAfterGB -
                    VramAverageBeforeGB;
            }
        }

        public double VramPeakBeforeGB { get; set; }

        public double VramPeakAfterGB { get; set; }

        public double VramPeakDeltaGB
        {
            get
            {
                return
                    VramPeakAfterGB -
                    VramPeakBeforeGB;
            }
        }

        // ============================================================
        // BENCHMARK DURATION
        // ============================================================

        public double BaselineDurationSeconds { get; set; }

        public double AfterDurationSeconds { get; set; }

        public double DurationDeltaSeconds
        {
            get
            {
                return
                    AfterDurationSeconds -
                    BaselineDurationSeconds;
            }
        }

        // ============================================================
        // STATUS
        // ============================================================

        public bool IsValid { get; set; }

        public string Status { get; set; } =
            "Invalid";

        public string Summary { get; set; } =
            "";

        // ============================================================
        // FACTORY
        // ============================================================

        public static BenchmarkComparison
            Create(
                string optimizationTitle,
                BenchmarkResult baseline,
                BenchmarkResult after)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(
                    nameof(baseline));
            }

            if (after == null)
            {
                throw new ArgumentNullException(
                    nameof(after));
            }

            var comparison =
                new BenchmarkComparison
                {
                    ComparisonId =
                        Guid.NewGuid().ToString("N"),

                    CreatedAt =
                        DateTime.Now,

                    OptimizationTitle =
                        optimizationTitle ?? "",

                    BaselineBenchmarkId =
                        baseline.BenchmarkId,

                    AfterBenchmarkId =
                        after.BenchmarkId,

                    CpuAverageBefore =
                        baseline.CpuAverageUsage,

                    CpuAverageAfter =
                        after.CpuAverageUsage,

                    CpuPeakBefore =
                        baseline.CpuPeakUsage,

                    CpuPeakAfter =
                        after.CpuPeakUsage,

                    RamAverageBefore =
                        baseline.RamAverageUsage,

                    RamAverageAfter =
                        after.RamAverageUsage,

                    RamPeakBefore =
                        baseline.RamPeakUsage,

                    RamPeakAfter =
                        after.RamPeakUsage,

                    DiskAverageBefore =
                        baseline.DiskAverageActivity,

                    DiskAverageAfter =
                        after.DiskAverageActivity,

                    DiskPeakBefore =
                        baseline.DiskPeakActivity,

                    DiskPeakAfter =
                        after.DiskPeakActivity,

                    GpuAverageBefore =
                        baseline.GpuAverageUsage,

                    GpuAverageAfter =
                        after.GpuAverageUsage,

                    GpuPeakBefore =
                        baseline.GpuPeakUsage,

                    GpuPeakAfter =
                        after.GpuPeakUsage,

                    GpuClockBeforeMHz =
                        baseline.GpuAverageClockMHz,

                    GpuClockAfterMHz =
                        after.GpuAverageClockMHz,

                    GpuTemperatureBeforeC =
                        baseline.GpuAverageTemperature,

                    GpuTemperatureAfterC =
                        after.GpuAverageTemperature,

                    VramAverageBeforeGB =
                        baseline.GpuAverageMemoryUsedGB,

                    VramAverageAfterGB =
                        after.GpuAverageMemoryUsedGB,

                    VramPeakBeforeGB =
                        baseline.GpuPeakMemoryUsedGB,

                    VramPeakAfterGB =
                        after.GpuPeakMemoryUsedGB,

                    BaselineDurationSeconds =
                        baseline.DurationSeconds,

                    AfterDurationSeconds =
                        after.DurationSeconds,

                    IsValid =
                        true,

                    Status =
                        "Complete"
                };

            comparison.Summary =
                comparison.BuildSummary();

            return comparison;
        }

        // ============================================================
        // SUMMARY
        // ============================================================

        private string BuildSummary()
        {
            return
                $"Telemetry comparison completed for " +
                $"{OptimizationTitle}. " +
                $"CPU average changed by " +
                $"{CpuAverageDelta:+0.0;-0.0;0.0} percentage points, " +
                $"GPU average changed by " +
                $"{GpuAverageDelta:+0.0;-0.0;0.0} percentage points, " +
                $"GPU temperature changed by " +
                $"{GpuTemperatureDeltaC:+0.0;-0.0;0.0}°C, " +
                $"and average VRAM usage changed by " +
                $"{VramAverageDeltaGB:+0.00;-0.00;0.00} GB.";
        }
    }
}