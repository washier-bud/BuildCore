using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public sealed class OptimizationTweakHandler
    {
        public string TweakId { get; }
        public string HandlerId { get; }
        public string Description { get; }
        public bool RequiresReboot { get; }
        public bool RollbackSupported { get; }

        private readonly Func<OptimizationRecommendation?> _createRecommendation;

        public OptimizationTweakHandler(
            string tweakId,
            string handlerId,
            string description,
            bool requiresReboot,
            bool rollbackSupported,
            Func<OptimizationRecommendation?> createRecommendation)
        {
            TweakId = tweakId;
            HandlerId = handlerId;
            Description = description;
            RequiresReboot = requiresReboot;
            RollbackSupported = rollbackSupported;
            _createRecommendation = createRecommendation;
        }

        public OptimizationRecommendation? CreateRecommendation()
        {
            return _createRecommendation();
        }
    }

    public static class OptimizationTweakHandlerRegistry
    {
        private static readonly IReadOnlyList<OptimizationTweakHandler> Handlers =
            new List<OptimizationTweakHandler>
            {
                new OptimizationTweakHandler(
                    "windows-game-mode",
                    "windows-game-mode-v1",
                    "Uses the existing verified Windows Game Mode optimization path.",
                    false,
                    true,
                    CreateGameModeRecommendation),

                new OptimizationTweakHandler(
                    "gaming-game-mode",
                    "windows-game-mode-v1",
                    "Uses the existing verified Windows Game Mode optimization path.",
                    false,
                    true,
                    CreateGameModeRecommendation),

                new OptimizationTweakHandler(
                    "high-performance",
                    "high-performance-power-plan-v1",
                    "Uses the existing verified High Performance power-plan optimization path.",
                    false,
                    true,
                    CreateHighPerformanceRecommendation)
            };

        public static IReadOnlyList<OptimizationTweakHandler> GetHandlers()
        {
            return Handlers;
        }

        public static IReadOnlyList<string> ValidateRegistry()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handlerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (OptimizationTweakHandler handler in Handlers)
            {
                if (handler == null)
                {
                    errors.Add("Registry contains a null handler.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(handler.TweakId))
                    errors.Add("Handler has an empty tweak ID.");

                if (string.IsNullOrWhiteSpace(handler.HandlerId))
                    errors.Add($"Handler for '{handler.TweakId}' has an empty handler ID.");

                if (string.IsNullOrWhiteSpace(handler.Description))
                    errors.Add($"Handler '{handler.HandlerId}' has an empty description.");

                if (!ids.Add(handler.TweakId))
                    errors.Add($"Duplicate tweak ID: '{handler.TweakId}'.");

                if (!handlerIds.Add(handler.HandlerId))
                {
                    // Shared handler IDs are allowed when multiple library tweaks
                    // intentionally use the same verified implementation.
                    if (!Handlers.Any(
                        existing =>
                            !ReferenceEquals(existing, handler) &&
                            existing.HandlerId.Equals(
                                handler.HandlerId,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"Duplicate handler ID: '{handler.HandlerId}'.");
                    }
                }
            }

            foreach (OptimizationTweakHandler handler in Handlers)
            {
                OptimizationTweakDefinition? definition =
                    OptimizationLibrary.Groups
                        .SelectMany(group => group.Tweaks)
                        .FirstOrDefault(
                            tweak => tweak.Id.Equals(
                                handler.TweakId,
                                StringComparison.OrdinalIgnoreCase));

                if (definition == null)
                {
                    errors.Add(
                        $"Handler '{handler.HandlerId}' references unknown tweak '{handler.TweakId}'.");
                    continue;
                }

                if (definition.RequiresReboot != handler.RequiresReboot)
                {
                    errors.Add(
                        $"Handler '{handler.HandlerId}' reboot metadata does not match tweak '{handler.TweakId}'.");
                }

                if (definition.RollbackSupported != handler.RollbackSupported)
                {
                    errors.Add(
                        $"Handler '{handler.HandlerId}' rollback metadata does not match tweak '{handler.TweakId}'.");
                }
            }

            return errors;
        }

        public static OptimizationTweakHandler? Find(string tweakId)
        {
            if (string.IsNullOrWhiteSpace(tweakId))
                return null;

            if (ValidateRegistry().Count > 0)
                return null;

            return Handlers.FirstOrDefault(
                handler => handler.TweakId.Equals(
                    tweakId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static OptimizationRecommendation? CreateGameModeRecommendation()
        {
            WindowsSystemData system = WindowsSystemService.Scan();

            return OptimizationAnalyzer.Analyze(system)
                .FirstOrDefault(
                    recommendation =>
                        recommendation.Title.Equals(
                            "Enable Windows Game Mode",
                            StringComparison.OrdinalIgnoreCase));
        }

        private static OptimizationRecommendation? CreateHighPerformanceRecommendation()
        {
            WindowsSystemData system = WindowsSystemService.Scan();

            return OptimizationAnalyzer.Analyze(system)
                .FirstOrDefault(
                    recommendation =>
                        recommendation.Title.Equals(
                            "Performance Power Plan",
                            StringComparison.OrdinalIgnoreCase));
        }
    }
}
