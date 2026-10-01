using System;
using System.Collections.Generic;

namespace BuildCore
{
    public static class RebootOptimizationRecoveryHandlerRegistry
    {
        private static readonly IReadOnlyList<IRebootOptimizationRecoveryHandler> Handlers =
            new IRebootOptimizationRecoveryHandler[] { new HagsRecoveryHandler() };

        public static IReadOnlyList<string> GetRegisteredTitles()
        {
            var titles = new List<string>();

            foreach (IRebootOptimizationRecoveryHandler handler in Handlers)
            {
                if (!string.IsNullOrWhiteSpace(handler.OptimizationTitle))
                    titles.Add(handler.OptimizationTitle);
            }

            return titles.AsReadOnly();
        }

        public static IRebootOptimizationRecoveryHandler? Find(
            RebootOptimizationExperimentState experiment)
        {
            return experiment == null
                ? null
                : Find(experiment.OptimizationTitle);
        }

        public static IRebootOptimizationRecoveryHandler? Find(
            string optimizationTitle)
        {
            if (string.IsNullOrWhiteSpace(optimizationTitle))
                return null;

            foreach (IRebootOptimizationRecoveryHandler handler in Handlers)
            {
                if (string.Equals(
                    handler.OptimizationTitle,
                    optimizationTitle,
                    StringComparison.Ordinal))
                {
                    return handler;
                }
            }

            return null;
        }
    }
}