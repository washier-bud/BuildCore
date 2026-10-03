using System;
using System.Collections.Generic;

namespace BuildCore
{
    public static class RebootOptimizationRecoveryHandlerRegistry
    {
        private static readonly IReadOnlyList<IRebootOptimizationRecoveryHandler> Handlers =
            Array.AsReadOnly(
                new IRebootOptimizationRecoveryHandler[]
                {
                    new HagsRecoveryHandler()
                });

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

        public static IReadOnlyList<string> ValidateRegistry()
        {
            var errors = new List<string>();
            var seenTitles = new HashSet<string>(StringComparer.Ordinal);
            var seenHandlerIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < Handlers.Count; i++)
            {
                IRebootOptimizationRecoveryHandler? handler = Handlers[i];

                if (handler == null)
                {
                    errors.Add($"Recovery handler at index {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(handler.OptimizationTitle))
                {
                    errors.Add(
                        $"Recovery handler at index {i} has an empty optimization title.");
                    continue;
                }

                if (!seenTitles.Add(handler.OptimizationTitle))
                {
                    errors.Add(
                        $"Duplicate recovery handler title: '{handler.OptimizationTitle}'.");
                }

                if (string.IsNullOrWhiteSpace(handler.HandlerId))
                {
                    errors.Add(
                        $"Recovery handler '{handler.OptimizationTitle}' has an empty handler ID.");
                }
                else if (!seenHandlerIds.Add(handler.HandlerId))
                {
                    errors.Add(
                        $"Duplicate recovery handler ID: '{handler.HandlerId}'.");
                }

                if (handler.HandlerVersion <= 0)
                {
                    errors.Add(
                        $"Recovery handler '{handler.OptimizationTitle}' has an invalid handler version.");
                }
            }

            return errors.AsReadOnly();
        }

        public static IRebootOptimizationRecoveryHandler? Find(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                return null;

            IRebootOptimizationRecoveryHandler? handler =
                Find(experiment.OptimizationTitle);

            if (handler == null)
                return null;

            if (!string.Equals(
                    experiment.RecoveryHandlerId,
                    handler.HandlerId,
                    StringComparison.Ordinal) ||
                experiment.RecoveryHandlerVersion != handler.HandlerVersion)
            {
                return null;
            }

            return handler;
        }

        public static IRebootOptimizationRecoveryHandler? Find(
            string optimizationTitle)
        {
            if (string.IsNullOrWhiteSpace(optimizationTitle))
                return null;

            if (ValidateRegistry().Count > 0)
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