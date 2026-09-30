using System;

namespace BuildCore
{
    public class BenchmarkResult
    {
        // ============================================================
        // IDENTIFICATION
        // ============================================================

        public string BenchmarkId { get; set; } =
            Guid.NewGuid().ToString("N");

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } =
            "Completed";

        public string Summary { get; set; } =
            "";

        // ============================================================
        // TIMING
        // ============================================================

        public TimeSpan Duration { get; set; }

        public double DurationSeconds
        {
            get
            {
                return Duration.TotalSeconds;
            }
            set
            {
                Duration =
                    TimeSpan.FromSeconds(value);
            }
        }

        public int SampleCount { get; set; }

        // ============================================================
        // CPU
        // ============================================================

        public double CpuAverageUsage { get; set; }

        public double CpuPeakUsage { get; set; }

        public double CpuAverageClockMHz { get; set; }

        // ============================================================
        // RAM
        // ============================================================

        public double RamAverageUsage { get; set; }

        public double RamPeakUsage { get; set; }

        // ============================================================
        // DISK
        // ============================================================

        public double DiskAverageActivity { get; set; }

        public double DiskPeakActivity { get; set; }

        // ============================================================
        // GPU
        // ============================================================

        public double GpuAverageUsage { get; set; }

        public double GpuPeakUsage { get; set; }

        public double GpuAverageClockMHz { get; set; }

        public double GpuAverageTemperature { get; set; }

        public double GpuPeakTemperature { get; set; }

        public double GpuAverageMemoryUsedGB { get; set; }

        public double GpuPeakMemoryUsedGB { get; set; }

        // ============================================================
        // SCORE
        // ============================================================

        public double PerformanceScore { get; set; }

        // ============================================================
        // COMPATIBILITY PROPERTIES
        //
        // These allow older BuildCore benchmark code to continue
        // compiling while we transition to the new naming scheme.
        // ============================================================

        public double AverageCpuUsage
        {
            get => CpuAverageUsage;
            set => CpuAverageUsage = value;
        }

        public double PeakCpuUsage
        {
            get => CpuPeakUsage;
            set => CpuPeakUsage = value;
        }

        public double AverageRamUsage
        {
            get => RamAverageUsage;
            set => RamAverageUsage = value;
        }

        public double PeakRamUsage
        {
            get => RamPeakUsage;
            set => RamPeakUsage = value;
        }

        public double AverageDiskUsage
        {
            get => DiskAverageActivity;
            set => DiskAverageActivity = value;
        }

        public double PeakDiskUsage
        {
            get => DiskPeakActivity;
            set => DiskPeakActivity = value;
        }

        public double AverageGpuUsage
        {
            get => GpuAverageUsage;
            set => GpuAverageUsage = value;
        }

        public double PeakGpuUsage
        {
            get => GpuPeakUsage;
            set => GpuPeakUsage = value;
        }

        public double AverageGpuClockMHz
        {
            get => GpuAverageClockMHz;
            set => GpuAverageClockMHz = value;
        }

        public double AverageGpuTemperatureC
        {
            get => GpuAverageTemperature;
            set => GpuAverageTemperature = value;
        }

        public double PeakGpuTemperatureC
        {
            get => GpuPeakTemperature;
            set => GpuPeakTemperature = value;
        }

        public double AverageVramUsedGB
        {
            get => GpuAverageMemoryUsedGB;
            set => GpuAverageMemoryUsedGB = value;
        }

        public double PeakVramUsedGB
        {
            get => GpuPeakMemoryUsedGB;
            set => GpuPeakMemoryUsedGB = value;
        }
    }
}