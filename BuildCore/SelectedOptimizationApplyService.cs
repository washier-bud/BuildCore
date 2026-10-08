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
        public bool BatchSucceeded { get; init; }
        public bool BatchRolledBack { get; init; }
        public bool RollbackVerified { get; init; }
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
                throw new ArgumentException(
                    "No optimizations were selected.",
                    nameof(tweakIds));

            IReadOnlyList<string> registryErrors =
                OptimizationTweakHandlerRegistry.ValidateRegistry();

            if (registryErrors.Count > 0)
                throw new InvalidOperationException(
                    "Optimization handler registry is invalid: " +
                    string.Join(" | ", registryErrors));

            BuildCoreSnapshot snapshot = SnapshotService.CreateSnapshot();

            var items = new List<SelectedOptimizationApplyItem>();
            var appliedTransactions = new List<OptimizationTransaction>();

            bool batchSucceeded = true;
            bool batchRolledBack = false;
            bool rollbackVerified = true;
            bool hadApplyFailure = false;

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
                        Status = "FAILED — no registered handler."
                    });

                    batchSucceeded = false;
                    hadApplyFailure = true;
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

                    batchSucceeded = false;
                    hadApplyFailure = true;
                    continue;
                }

                if (!handler.CanApply)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "SKIPPED — handler is review-only."
                    });

                    continue;
                }

                if (!recommendation.CanApply)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "FAILED — current state could not be safely captured for this handler."
                    });

                    batchSucceeded = false;
                    hadApplyFailure = true;
                    continue;
                }

                try
                {
                    OptimizationTransaction transaction =
                        OptimizationTransactionService.CreateTransaction(
                            snapshot.Id,
                            recommendation);

                    OptimizationApplyResult result =
                        OptimizationApplyService.Apply(recommendation);

                    OptimizationTransactionService.CompleteTransaction(
                        transaction,
                        result);

                    bool appliedAndVerified =
                        result.Success && result.Verified;

                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Transaction = transaction,
                        Status =
                            appliedAndVerified
                                ? "APPLIED & VERIFIED"
                                : "FAILED — " + result.Message
                    });

                    if (appliedAndVerified)
                    {
                        appliedTransactions.Add(transaction);
                        continue;
                    }

                    batchSucceeded = false;
                    hadApplyFailure = true;
                }
                catch (Exception ex)
                {
                    items.Add(new SelectedOptimizationApplyItem
                    {
                        TweakId = tweak.Id,
                        Title = tweak.Title,
                        Status = "FAILED — " + ex.Message
                    });

                    batchSucceeded = false;
                    hadApplyFailure = true;
                }
            }

            if (hadApplyFailure && appliedTransactions.Count > 0)
            {
                (batchRolledBack, rollbackVerified) =
                    RollbackAppliedTransactions(
                        items,
                        appliedTransactions);
            }

            return new SelectedOptimizationApplyResult
            {
                SnapshotId = snapshot.Id,
                BatchSucceeded = batchSucceeded,
                BatchRolledBack = batchRolledBack,
                RollbackVerified = rollbackVerified,
                Items = items
            };
        }

        private static (bool RolledBack, bool Verified)
            RollbackAppliedTransactions(
                List<SelectedOptimizationApplyItem> items,
                List<OptimizationTransaction> appliedTransactions)
        {
            if (appliedTransactions.Count == 0)
                return (false, true);

            bool allRollbackSucceeded = true;
            bool allRollbackVerified = true;

            for (int index = appliedTransactions.Count - 1;
                 index >= 0;
                 index--)
            {
                OptimizationTransaction rollbackTransaction =
                    appliedTransactions[index];

                OptimizationRestoreResult rollbackResult =
                    OptimizationRestoreService.Restore(
                        rollbackTransaction);

                OptimizationTransactionService.CompleteRestore(
                    rollbackTransaction,
                    rollbackResult);

                bool rollbackSucceeded =
                    rollbackResult.Success &&
                    rollbackResult.Verified;

                if (!rollbackSucceeded)
                {
                    allRollbackSucceeded = false;
                    allRollbackVerified = false;
                }

                int itemIndex = items.FindIndex(
                    item =>
                        item.Transaction?.TransactionId ==
                        rollbackTransaction.TransactionId);

                if (itemIndex >= 0)
                {
                    SelectedOptimizationApplyItem appliedItem =
                        items[itemIndex];

                    string rollbackStatus =
                        rollbackSucceeded
                            ? "ROLLED BACK & VERIFIED"
                            : "ROLLBACK FAILED — " +
                              rollbackResult.Message;

                    items[itemIndex] =
                        new SelectedOptimizationApplyItem
                        {
                            TweakId = appliedItem.TweakId,
                            Title = appliedItem.Title,
                            Transaction = rollbackTransaction,
                            Status = appliedItem.Status +
                                     " → " +
                                     rollbackStatus
                        };
                }
            }

            return (
                allRollbackSucceeded,
                allRollbackVerified);
        }
    }
}
