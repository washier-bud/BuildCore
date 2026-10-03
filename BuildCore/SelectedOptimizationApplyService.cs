using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public sealed class SelectedOptimizationApplyItem
    {
        public string TweakId { get; init; } = "";
        public string Title { get; init; } = "";
        public string Status { get; init; } = "";
        public OptimizationTransaction? Transaction { get; init; }
    }

    public sealed class SelectedOptimizationApplyResult
    {
        public string SnapshotId { get; init; } = "";
        public IReadOnlyList<SelectedOptimizationApplyItem> Items { get; init; } =
            Array.Empty<SelectedOptimizationApplyItem>();
    }

    public static class SelectedOptimizationApplyService
    {
        public static SelectedOptimizationApplyResult Apply(
            IEnumerable<string> tweakIds)
        {
            if (tweakIds == null)
                throw new ArgumentNullException(nameof(tweakIds));

            var selectedIds =
                new HashSet<string>(
                    tweakIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                    StringComparer.OrdinalIgnoreCase);

            if (selectedIds.Count == 0)
                throw new ArgumentException("No optimizations were selected.", nameof(tweakIds));

            IReadOnlyList<string> registryErrors =
                OptimizationTweakHandlerRegistry.ValidateRegistry();

            if (registryErrors.Count > 0)
                throw new InvalidOperationException(
                    "Optimization handler registry is invalid: " +
                    string.Join(" | ", registryErrors));

            BuildCoreSnapshot snapshot = SnapshotService.CreateSnapshot();

            var items = new List<SelectedOptimizationApplyItem>();

            foreach (OptimizationTweakDefinition tweak in
                OptimizationLibrary.Groups
                    .SelectMany(group => group.Tweaks)
                    .Where(tweak => selectedIds.Contains(tweak.Id)))
            {
                OptimizationTweakHandler? handler =
                    OptimizationTweakHandlerRegistry.Find(tweak.Id);

                if (handler == null)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "SKIPPED — no registered handler."
                    });
                    continue;
                }

                OptimizationRecommendation? recommendation =
                    handler.CreateRecommendation();

                if (recommendation == null)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "FAILED — current state could not be analyzed."
                    });
                    break;
                }

                if (!recommendation.CanApply)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "SKIPPED — handler is review-only."
                    });
                    continue;
                }

                OptimizationTransaction transaction =
                    OptimizationTransactionService.CreateTransaction(
                        snapshot.Id,
                        recommendation);

                OptimizationApplyResult result =
                    OptimizationApplyService.Apply(recommendation);

                OptimizationTransactionService.CompleteTransaction(
                    transaction,
                    result);

                items.Add(new SelectedOptimizationApplyItem
                {
                    TweakId = tweak.Id,
                    Title = tweak.Title,
                    Transaction = transaction,
                    Status =
                        result.Success && result.Verified
                            ? "APPLIED & VERIFIED"
                            : "FAILED — " + result.Message
                });

                if (!result.Success || !result.Verified)
                    break;
            }

            return new SelectedOptimizationApplyResult
            {
                SnapshotId = snapshot.Id,
                Items = items
            };
        }
    }
}
