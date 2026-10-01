using System;

namespace BuildCore
{
    public enum RebootOptimizationExperimentPhase
    {
        NotStarted,
        BaselineCompleted,
        SnapshotCreated,
        OptimizationPendingReboot,
        RebootRequired,
        AfterRebootValidation,
        AfterBenchmarkPending,
        Completed,
        Inconclusive,
        Failed,
        Canceled
    }

    public class RebootOptimizationExperimentState
    {
        public string ExperimentId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? BaselineBootTimeUtc { get; set; }
        public DateTime? AfterRebootBootTimeUtc { get; set; }

        public bool RebootDetected { get; set; }
        public bool AfterRebootValidationPassed { get; set; }

        public RebootOptimizationExperimentPhase Phase { get; set; } =
            RebootOptimizationExperimentPhase.NotStarted;

        public string Status { get; set; } = "Not Started";
        public string OptimizationTitle { get; set; } = "";
        public string SnapshotId { get; set; } = "";

        public BenchmarkWorkload? WorkloadDefinition { get; set; }
        public string WorkloadFingerprint { get; set; } = "";

        public WorkloadBenchmarkResult? Baseline { get; set; }
        public WorkloadEnvironmentSnapshot? BaselineEnvironment { get; set; }

        public WorkloadBenchmarkResult? AfterBenchmark { get; set; }
        public WorkloadEnvironmentSnapshot? AfterEnvironment { get; set; }
        public WorkloadEnvironmentComparison? EnvironmentComparison { get; set; }
        public WorkloadStatisticalAnalysis? Analysis { get; set; }
        public WorkloadEvidenceQuality? EvidenceQuality { get; set; }

        public string ExpectedTargetProcessPath { get; set; } = "";
        public DateTime? ExpectedTargetProcessStartTimeUtc { get; set; }

        // Recovery metadata for optimizations with an explicit rollback handler.
        public bool RecoveryAvailable { get; set; }
        public bool RecoveryAttempted { get; set; }
        public bool RecoverySucceeded { get; set; }
        public DateTime? RecoveryAttemptedAtUtc { get; set; }
        public string RecoveryStatus { get; set; } = "No recovery attempted.";

        // HAGS HwSchMode preservation. A missing value represents the
        // Windows default state and must not be confused with DWORD 0.
        public bool OriginalHagsValueExists { get; set; }
        public int? OriginalHagsMode { get; set; }

        public bool BaselineCompleted { get; set; }
        public bool SnapshotCreated { get; set; }
        public bool OptimizationChangePending { get; set; }
        public bool RebootRequired { get; set; }
        public bool AfterBenchmarkCompleted { get; set; }
        public bool AnalysisCompleted { get; set; }
        public bool EvidenceGatePassed { get; set; }

        public bool IsPendingReboot =>
            Phase == RebootOptimizationExperimentPhase.OptimizationPendingReboot ||
            Phase == RebootOptimizationExperimentPhase.RebootRequired;

        public bool IsTerminal =>
            Phase == RebootOptimizationExperimentPhase.Completed ||
            Phase == RebootOptimizationExperimentPhase.Inconclusive ||
            Phase == RebootOptimizationExperimentPhase.Failed ||
            Phase == RebootOptimizationExperimentPhase.Canceled;
    }
}