using System;

namespace BuildCore
{
    public class OptimizationBenchmarkResult
    {
        public string TestId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string Status { get; set; } = "Not Started";
        public ControlledWorkloadTestState TestState { get; set; } = ControlledWorkloadTestState.NotStarted;
        public string OptimizationTitle { get; set; } = "";
        public string SnapshotId { get; set; } = "";
        public BenchmarkResult? Baseline { get; set; }
        public BenchmarkResult? After { get; set; }
        public BenchmarkComparison? Comparison { get; set; }
        public ReliableBenchmarkResult? BaselineReliable { get; set; }
        public ReliableBenchmarkResult? AfterReliable { get; set; }
        public OptimizationTestAnalysis? Analysis { get; set; }
        public OptimizationApplyResult? ApplyResult { get; set; }
        public BenchmarkWorkload? WorkloadDefinition { get; set; }
        public string WorkloadFingerprint { get; set; } = "";
        public WorkloadEnvironmentSnapshot? WorkloadBaselineEnvironment { get; set; }
        public WorkloadEnvironmentSnapshot? WorkloadAfterEnvironment { get; set; }
        public WorkloadEnvironmentComparison? WorkloadEnvironmentComparison { get; set; }
        public WorkloadEvidenceQuality? WorkloadEvidenceQuality { get; set; }

        // Phase 1.12Z: final evidence trust classification used by
        // measured optimization decisions and AutoTune.
        public BenchmarkEvidenceTrust? EvidenceTrust { get; set; }

        // Phase 1.12U: explicit gate indicating whether the workload result
        // meets BuildCore's minimum evidence requirements.
        public bool WorkloadEvidenceGatePassed { get; set; }
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
                    BaselineCompleted && SnapshotCreated &&
                    OptimizationApplied && OptimizationVerified &&
                    AfterBenchmarkCompleted && ComparisonCompleted &&
                    AnalysisCompleted && Comparison != null && Analysis != null;

                bool workloadSuccessful =
                    WorkloadBaselineCompleted && WorkloadSnapshotCreated &&
                    WorkloadOptimizationApplied && WorkloadOptimizationVerified &&
                    WorkloadAfterCompleted && WorkloadAnalysisCompleted &&
                    WorkloadBaseline != null && WorkloadAfter != null &&
                    WorkloadAnalysis != null && WorkloadAnalysis.IsComparable &&
                    WorkloadEvidenceQuality != null &&
                    WorkloadEvidenceGatePassed &&
                    WorkloadEvidenceQuality.IsSufficient &&
                    !string.IsNullOrWhiteSpace(WorkloadFingerprint) &&
                    string.Equals(WorkloadFingerprint, WorkloadBaseline.WorkloadFingerprint, StringComparison.Ordinal) &&
                    string.Equals(WorkloadFingerprint, WorkloadAfter.WorkloadFingerprint, StringComparison.Ordinal);

                return legacySuccessful || workloadSuccessful;
            }
        }

        public BenchmarkEvidenceTrust EvaluateEvidenceTrust()
        {
            if (WorkloadEvidenceQuality == null)
            {
                EvidenceTrust = BenchmarkEvidenceTrust.Evaluate(this);
                return EvidenceTrust;
            }

            EvidenceTrust = BenchmarkEvidenceTrust.Evaluate(this);
            return EvidenceTrust;
        }

        public TimeSpan Duration =>
            CompletedAt == default || StartedAt == default
                ? TimeSpan.Zero
                : CompletedAt - StartedAt;
    }
}