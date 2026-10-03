using System;

namespace BuildCore
{
    public interface IRebootOptimizationRecoveryHandler
    {
        string OptimizationTitle { get; }

        string HandlerId { get; }

        int HandlerVersion { get; }

        void CaptureOriginalState(RebootOptimizationExperimentState experiment);

        bool CanRollback(RebootOptimizationExperimentState experiment);

        OptimizationApplyResult Rollback(RebootOptimizationExperimentState experiment);

        bool VerifyRestoredState(RebootOptimizationExperimentState experiment);
    }
}
