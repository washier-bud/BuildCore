using System;
using System.Collections.Generic;

namespace BuildCore
{
    public static class RebootOptimizationRecoveryHandlerRegistry
    {
        private static readonly IReadOnlyList<IRebootOptimizationRecoveryHandler> Handlers =
            new IRebootOptimizationRecoveryHandler[] { new HagsRecoveryHandler() };

        public static IRebootOptimizationRecoveryHandler? Find(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null) return null;
            foreach (IRebootOptimizationRecoveryHandler handler in Handlers)
            {
                if (string.Equals(handler.OptimizationTitle, experiment.OptimizationTitle, StringComparison.Ordinal))
                    return handler;
            }
            return null;
        }
    }
}