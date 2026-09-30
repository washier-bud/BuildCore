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
        public string WorkloadFingerprint { get; set; } = "";
        public WorkloadEnvironmentSnapshot? Environment { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string Status { get; set; } = "Incomplete";
        public string Summary { get; set; } = "";
        public List<BenchmarkRun> Runs { get; set; } = new();
        public int RequestedRuns { get; set; }
        public int CompletedRuns => Runs.Count(run => run != null && run.IsSuccessful);
        public TimeSpan Duration =>
            CompletedAt >= StartedAt && StartedAt != default
                ? CompletedAt - StartedAt
                : TimeSpan.Zero;
        public bool IsComplete => RequestedRuns > 0 && CompletedRuns == RequestedRuns;
        public bool HasEnoughRuns => CompletedRuns >= 3;
        public string ReliabilityStatus =>
            !IsComplete ? "Incomplete" :
            !HasEnoughRuns ? "Limited Data" :
            "Reliable";
    }
}