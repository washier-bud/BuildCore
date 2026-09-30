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

        public ControlledWorkloadTestState TestState { get; set; } =
            ControlledWorkloadTestState.NotStarted;

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

        // Phase 1.12I workload evidence. These fields preserve the exact
        // workload used before and after the optimization so the result can
        // be reviewed after the application is restarted.
        public BenchmarkWorkload? WorkloadDefinition { get; set; }

        public string WorkloadFingerprint { get; set; } = "";

        public WorkloadEnvironmentSnapshot? WorkloadBaselineEnvironment { get; set; }

        public WorkloadEnvironmentSnapshot? WorkloadAfterEnvironment { get; set; }

        public WorkloadEnvironmentComparison? WorkloadEnvironmentComparison { get; set; }

        public WorkloadBenchmarkResult? WorkloadBaseline { get; set; }

        public WorkloadBenchmarkResult? WorkloadAfter { get; set; }

        public WorkloadStatisticalAnalysis? WorkloadAnalysis { get; set; }

        public bool WorkloadBaselineCompleted { get; set; }

        public bool WorkloadSnapshotCreated { get; set; }

        public bool WorkloadOptimizationApplied { get; set; }

        public bool WorkloadOptimizationVerified { get; set; }

        public bool WorkloadAfterCompleted { get; set; }

        public bool WorkloadAnalysisCompleted { get; set; }

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
                bool legacySuccessful =
                    BaselineCompleted &&
                    SnapshotCreated &&
                    OptimizationApplied &&
                    OptimizationVerified &&
                    AfterBenchmarkCompleted &&
                    ComparisonCompleted &&
                    AnalysisCompleted &&
                    Comparison != null &&
                    Analysis != null;

                bool workloadSuccessful =
                    WorkloadBaselineCompleted &&
                    WorkloadSnapshotCreated &&
                    WorkloadOptimizationApplied &&
                    WorkloadOptimizationVerified &&
                    WorkloadAfterCompleted &&
                    WorkloadAnalysisCompleted &&
                    WorkloadBaseline != null &&
                    WorkloadAfter != null &&
                    WorkloadAnalysis != null &&
                    WorkloadAnalysis.IsComparable &&
                    !string.IsNullOrWhiteSpace(WorkloadFingerprint) &&
                    string.Equals(WorkloadFingerprint, WorkloadBaseline.WorkloadFingerprint, StringComparison.Ordinal) &&
                    string.Equals(WorkloadFingerprint, WorkloadAfter.WorkloadFingerprint, StringComparison.Ordinal);

                return legacySuccessful || workloadSuccessful;
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