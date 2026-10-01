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

        public RebootOptimizationExperimentPhase Phase { get; set; } =
            RebootOptimizationExperimentPhase.NotStarted;

        public string Status { get; set; } = "Not Started";
        public string OptimizationTitle { get; set; } = "";
        public string SnapshotId { get; set; } = "";

        public BenchmarkWorkload? WorkloadDefinition { get; set; }
        public string WorkloadFingerprint { get; set; } = "";

        public WorkloadBenchmarkResult? Baseline { get; set; }
        public WorkloadEnvironmentSnapshot? BaselineEnvironment { get; set; }

        public string ExpectedTargetProcessPath { get; set; } = "";
        public DateTime? ExpectedTargetProcessStartTimeUtc { get; set; }

        public bool BaselineCompleted { get; set; }
        public bool SnapshotCreated { get; set; }
        public bool OptimizationChangePending { get; set; }
        public bool RebootRequired { get; set; }

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