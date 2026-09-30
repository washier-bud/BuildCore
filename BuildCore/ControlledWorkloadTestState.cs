using System;

namespace BuildCore
{
    public enum ControlledWorkloadTestState
    {
        NotStarted,
        Preparing,
        BaselineRunning,
        BaselineValidated,
        SnapshotCreated,
        OptimizationApplying,
        OptimizationVerified,
        Settling,
        AfterRunning,
        AfterValidated,
        AnalysisRunning,
        Completed,
        Inconclusive,
        Failed,
        Canceled
    }
}