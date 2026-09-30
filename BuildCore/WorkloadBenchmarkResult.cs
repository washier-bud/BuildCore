using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public class WorkloadBenchmarkResult
    {
        public string ResultId { get; set; } = Guid.NewGuid().ToString("N");

        public string WorkloadId { get; set; } = "";

        public string WorkloadName { get; set; } = "";

        public BenchmarkWorkloadType WorkloadType { get; set; } = BenchmarkWorkloadType.Custom;

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } = "Incomplete";

        public string Summary { get; set; } = "";

        public List<BenchmarkRun> Runs { get; set; } = new();

        public int RequestedRuns { get; set; }

        public int CompletedRuns => Runs.Count(r => r != null && r.IsSuccessful);

        public TimeSpan Duration => CompletedAt >= StartedAt
            ? CompletedAt - StartedAt
            : TimeSpan.Zero;

        public bool IsComplete =>
            RequestedRuns > 0 &&
            CompletedRuns >= RequestedRuns &&
            Runs.Count >= RequestedRuns &&
            Runs.Take(RequestedRuns).All(r => r != null && r.IsSuccessful);

        public bool HasEnoughRuns => CompletedRuns >= 3;

        public string ReliabilityStatus
        {
            get
            {
                if (!IsComplete)
                    return "Incomplete";

                if (CompletedRuns < 3)
                    return "Limited Data";

                return "Reliable";
            }
        }
    }
}
