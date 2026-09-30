using System;

namespace BuildCore
{
    public class BenchmarkRun
    {
        public string RunId { get; set; } =
            Guid.NewGuid().ToString("N");

        public int RunNumber { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } =
            "Not Started";

        public double CpuAverageUsage { get; set; }

        public double CpuPeakUsage { get; set; }

        public double RamAverageUsage { get; set; }

        public double RamPeakUsage { get; set; }

        public double DiskAverageActivity { get; set; }

        public double DiskPeakActivity { get; set; }

        public double GpuAverageUsage { get; set; }

        public double GpuPeakUsage { get; set; }

        public double GpuAverageClockMHz { get; set; }

        public double GpuAverageTemperature { get; set; }

        public double GpuPeakTemperature { get; set; }

        public double GpuAverageMemoryUsedGB { get; set; }

        public double GpuPeakMemoryUsedGB { get; set; }

        // Real frame-time measurements are populated only when
        // the workload provides an actual frame-time data source.
        public FrameTimeStatistics? FrameTime { get; set; }

        // Environment captured for this individual run.
        public WorkloadEnvironmentSnapshot? Environment { get; set; }

        // Process identity captured for interactive workloads.
        public RunningProcessInfo? TargetProcessIdentity { get; set; }

        public double DurationSeconds { get; set; }

        public int SampleCount { get; set; }

        public TimeSpan Duration
        {
            get
            {
                if (CompletedAt == default ||
                    StartedAt == default)
                {
                    return TimeSpan.Zero;
                }

                return CompletedAt - StartedAt;
            }
        }

        public bool IsSuccessful
        {
            get
            {
                return
                    Status.Equals(
                        "Completed",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    SampleCount > 0
                    &&
                    DurationSeconds > 0;
            }
        }
    }
}
