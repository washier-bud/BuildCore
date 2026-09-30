using System;

namespace BuildCore
{
    public class WorkloadRunResult
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString("N");

        public string WorkloadId { get; set; } = "";

        public string WorkloadName { get; set; } = "";

        public BenchmarkWorkloadType WorkloadType { get; set; } =
            BenchmarkWorkloadType.Custom;

        public BenchmarkRun? BenchmarkRun { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } = "Incomplete";

        public string Summary { get; set; } = "";

        public bool IsSuccessful =>
            Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) &&
            BenchmarkRun != null &&
            BenchmarkRun.IsSuccessful;
    }
}
