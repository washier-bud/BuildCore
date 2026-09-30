using System;

namespace BuildCore
{
    public class OptimizationBenchmarkResult
    {
        public string TestId { get; set; } =
            Guid.NewGuid().ToString("N");

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } =
            "Not Started";

        public string OptimizationTitle { get; set; } =
            "";

        public string SnapshotId { get; set; } =
            "";

        public BenchmarkResult? Baseline { get; set; }

        public BenchmarkResult? After { get; set; }

        public BenchmarkComparison? Comparison { get; set; }

        // Reliable multi-run benchmark data used for statistical analysis.
        // Keeping the original reliable results lets the History/Test Details UI
        // show run counts and consistency information after the app is restarted.
        public ReliableBenchmarkResult? BaselineReliable { get; set; }

        public ReliableBenchmarkResult? AfterReliable { get; set; }

        public OptimizationTestAnalysis? Analysis { get; set; }

        public OptimizationApplyResult? ApplyResult { get; set; }

        public bool BaselineCompleted { get; set; }

        public bool SnapshotCreated { get; set; }

        public bool OptimizationApplied { get; set; }

        public bool OptimizationVerified { get; set; }

        public bool AfterBenchmarkCompleted { get; set; }

        public bool ComparisonCompleted { get; set; }

        public bool AnalysisCompleted { get; set; }

        public bool IsSuccessful
        {
            get
            {
                return
                    BaselineCompleted &&
                    SnapshotCreated &&
                    OptimizationApplied &&
                    OptimizationVerified &&
                    AfterBenchmarkCompleted &&
                    ComparisonCompleted &&
                    AnalysisCompleted &&
                    Comparison != null &&
                    Analysis != null;
            }
        }

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
    }
}