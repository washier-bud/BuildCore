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

        // System boot time captured before the reboot.
        public DateTime? BaselineBootTimeUtc { get; set; }

        // Boot time observed when BuildCore resumes after restart.
        public DateTime? AfterRebootBootTimeUtc { get; set; }

        // Validation results recorded before the experiment continues.
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