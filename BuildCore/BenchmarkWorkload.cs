using System;

namespace BuildCore
{
    public class BenchmarkWorkload
    {
        public string WorkloadId { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "Custom Workload";

        public BenchmarkWorkloadType Type { get; set; } = BenchmarkWorkloadType.Custom;

        public string Description { get; set; } = "";

        public int DurationSeconds { get; set; } = 5;

        public int RunCount { get; set; } = 3;

        public int SampleIntervalMilliseconds { get; set; } = 100;

        public int DelayBetweenRunsMilliseconds { get; set; } = 500;

        public bool RequiresInteractiveWorkload { get; set; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(WorkloadId) &&
            !string.IsNullOrWhiteSpace(Name) &&
            DurationSeconds > 0 &&
            RunCount > 0 &&
            SampleIntervalMilliseconds > 0 &&
            DelayBetweenRunsMilliseconds >= 0;
    }
}
