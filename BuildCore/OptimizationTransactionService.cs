using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BuildCore
{
    public static class OptimizationTransactionService
    {
        private static readonly string TransactionDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BuildCore",
                "Transactions");

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        public static OptimizationTransaction CreateTransaction(
            string snapshotId,
            OptimizationRecommendation recommendation)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new ArgumentException(
                    "Snapshot ID cannot be empty.",
                    nameof(snapshotId));
            }

            if (recommendation == null)
            {
                throw new ArgumentNullException(
                    nameof(recommendation));
            }

            Directory.CreateDirectory(
                TransactionDirectory);

            return new OptimizationTransaction
            {
                TransactionId =
                    Guid.NewGuid().ToString("N"),

                SnapshotId =
                    snapshotId,

                CreatedAt =
                    DateTime.Now,

                OptimizationTitle =
                    recommendation.Title,

                Category =
                    recommendation.Category,

                BeforeValue =
                    recommendation.CurrentValue,

                TargetValue =
                    recommendation.RecommendedValue,

                ApplySucceeded =
                    false,

                VerificationSucceeded =
                    false,

                ApplyMessage =
                    "Transaction created. Optimization has not been applied.",

                RestoreAvailable =
                    false,

                RestoreSucceeded =
                    false,

                RestoreVerificationSucceeded =
                    false,

                RestoreMessage =
                    "Restore has not been performed."
            };
        }

        public static void CompleteTransaction(
            OptimizationTransaction transaction,
            OptimizationApplyResult result)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(
                    nameof(transaction));
            }

            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            transaction.ApplySucceeded =
                result.Success;

            transaction.VerificationSucceeded =
                result.Verified;

            transaction.ApplyMessage =
                result.Message ?? "";

            transaction.Error =
                result.Error;

            transaction.RestoreAvailable =
                result.Success &&
                result.Verified &&
                HasRestoreHandler(
                    transaction.OptimizationTitle);

            transaction.RestoreSucceeded =
                false;

            transaction.RestoreVerificationSucceeded =
                false;

            transaction.RestoreMessage =
                transaction.RestoreAvailable
                    ? "Restore is available."
                    : "Restore is not available for this optimization.";

            SaveTransaction(
                transaction);
        }

        public static void CompleteRestore(
            OptimizationTransaction transaction,
            OptimizationRestoreResult result)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(
                    nameof(transaction));
            }

            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            transaction.RestoreSucceeded =
                result.Success;

            transaction.RestoreVerificationSucceeded =
                result.Verified;

            transaction.RestoreMessage =
                result.Message ?? "";

            transaction.RestoreError =
                result.Error;

            if (result.Success &&
                result.Verified)
            {
                transaction.RestoredAt =
                    DateTime.Now;

                transaction.RestoreAvailable =
                    false;
            }

            SaveTransaction(
                transaction);
        }

        public static bool HasRestoreHandler(
            string optimizationTitle)
        {
            if (RegistryOptimizationHandler.IsSupported(optimizationTitle))
            {
                return true;
            }

            return optimizationTitle switch
            {
                "Disable adapter power saving" => true,
                "Performance Power Plan" => true,
                "Enable Windows Game Mode" => true,
                "Prevent sleep during workload" => true,
                "Workload display timeout" => true,
                _ => false
            };
        }

        public static void SaveTransaction(
            OptimizationTransaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(
                    nameof(transaction));
            }

            Directory.CreateDirectory(
                TransactionDirectory);

            string filePath =
                GetTransactionPath(
                    transaction.TransactionId);

            string json =
                JsonSerializer.Serialize(
                    transaction,
                    JsonOptions);

            string temporaryPath =
                filePath + ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    json);

                if (File.Exists(filePath))
                {
                    File.Replace(
                        temporaryPath,
                        filePath,
                        null);
                }
                else
                {
                    File.Move(
                        temporaryPath,
                        filePath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // Preserve the original persistence exception.
                }

                throw;
            }
        }

        public static OptimizationTransaction?
            LoadTransaction(
                string transactionId)
        {
            try
            {
                string filePath =
                    GetTransactionPath(
                        transactionId);

                if (!File.Exists(filePath))
                {
                    return null;
                }

                string json =
                    File.ReadAllText(
                        filePath);

                return JsonSerializer.Deserialize
                    <OptimizationTransaction>(
                        json,
                        JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static List<OptimizationTransaction>
            GetTransactions()
        {
            var transactions =
                new List<OptimizationTransaction>();

            if (!Directory.Exists(
                TransactionDirectory))
            {
                return transactions;
            }

            foreach (string file in
                Directory.GetFiles(
                    TransactionDirectory,
                    "*.json"))
            {
                try
                {
                    string json =
                        File.ReadAllText(file);

                    OptimizationTransaction?
                        transaction =
                            JsonSerializer.Deserialize
                            <OptimizationTransaction>(
                                json,
                                JsonOptions);

                    if (transaction == null)
                    {
                        continue;
                    }

                    transactions.Add(
                        transaction);
                }
                catch
                {
                    // Ignore invalid transaction files.
                }
            }

            return transactions
                .OrderByDescending(
                    transaction =>
                        transaction.CreatedAt)
                .ToList();
        }

        public static bool DeleteTransaction(
            string transactionId)
        {
            try
            {
                string filePath =
                    GetTransactionPath(
                        transactionId);

                if (!File.Exists(filePath))
                {
                    return false;
                }

                File.Delete(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateTransaction(
            string transactionId)
        {
            try
            {
                OptimizationTransaction?
                    transaction =
                        LoadTransaction(
                            transactionId);

                if (transaction == null)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                    transaction.TransactionId))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                    transaction.SnapshotId))
                {
                    return false;
                }

                if (transaction.CreatedAt ==
                    default)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                    transaction.OptimizationTitle))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                    transaction.BeforeValue))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                    transaction.TargetValue))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string
            GetTransactionDirectory()
        {
            Directory.CreateDirectory(
                TransactionDirectory);

            return TransactionDirectory;
        }

        private static string
            GetTransactionPath(
                string transactionId)
        {
            if (string.IsNullOrWhiteSpace(
                transactionId) ||
                transactionId.Any(
                    character =>
                        !char.IsLetterOrDigit(
                            character)))
            {
                throw new ArgumentException(
                    "Invalid transaction ID.",
                    nameof(transactionId));
            }

            return Path.Combine(
                TransactionDirectory,
                $"{transactionId}.json");
        }
    }
}