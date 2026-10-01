using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BuildCore
{
    public static class RebootOptimizationExperimentStorageService
    {
        private static readonly string ExperimentDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BuildCore",
                "RebootOptimizationExperiments");

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        public static void Save(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            ValidateId(experiment.ExperimentId);

            Directory.CreateDirectory(ExperimentDirectory);

            experiment.UpdatedAt = DateTime.Now;

            if (experiment.IsPendingReboot &&
                !experiment.BaselineBootTimeUtc.HasValue)
            {
                experiment.BaselineBootTimeUtc =
                    RebootOptimizationExperimentValidationService.GetCurrentBootTimeUtc();
            }

            string path = GetPath(experiment.ExperimentId);
            string json = JsonSerializer.Serialize(experiment, JsonOptions);

            File.WriteAllText(path, json);
        }

        public static RebootOptimizationExperimentState? Load(
            string experimentId)
        {
            try
            {
                ValidateId(experimentId);

                string path = GetPath(experimentId);

                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);

                return JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                    json,
                    JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static List<RebootOptimizationExperimentState> GetExperiments()
        {
            var experiments =
                new List<RebootOptimizationExperimentState>();

            if (!Directory.Exists(ExperimentDirectory))
                return experiments;

            foreach (string file in Directory.GetFiles(
                ExperimentDirectory,
                "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);

                    RebootOptimizationExperimentState? experiment =
                        JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                            json,
                            JsonOptions);

                    if (experiment == null ||
                        string.IsNullOrWhiteSpace(experiment.ExperimentId))
                    {
                        continue;
                    }

                    experiments.Add(experiment);
                }
                catch
                {
                    // Ignore corrupted experiment files.
                }
            }

            return experiments
                .OrderByDescending(e => e.UpdatedAt)
                .ToList();
        }

        public static RebootOptimizationExperimentState? GetPending()
        {
            return GetExperiments()
                .FirstOrDefault(e => e.IsPendingReboot);
        }

        public static bool Delete(string experimentId)
        {
            try
            {
                ValidateId(experimentId);

                string path = GetPath(experimentId);

                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Validate(string experimentId)
        {
            try
            {
                RebootOptimizationExperimentState? experiment =
                    Load(experimentId);

                if (experiment == null ||
                    string.IsNullOrWhiteSpace(experiment.ExperimentId) ||
                    experiment.ExperimentId != experimentId ||
                    experiment.CreatedAt == default ||
                    experiment.UpdatedAt == default ||
                    experiment.UpdatedAt < experiment.CreatedAt ||
                    string.IsNullOrWhiteSpace(experiment.OptimizationTitle))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.RebootRequired &&
                    (!experiment.RecoverySucceeded ||
                     !experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized ||
                     !experiment.RecoveryAttemptedAtUtc.HasValue))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Finalized &&
                    (!experiment.RecoverySucceeded ||
                     experiment.RecoveryRequiresReboot ||
                     !experiment.RecoveryFinalized ||
                     !experiment.RecoveryFinalizedAtUtc.HasValue))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Available &&
                    (!experiment.RecoveryAvailable || experiment.RecoveryAttempted ||
                     experiment.RecoverySucceeded || experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.None &&
                    (experiment.RecoveryAvailable ||
                     experiment.RecoveryAttempted ||
                     experiment.RecoverySucceeded ||
                     experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized ||
                     experiment.RecoveryAttemptedAtUtc.HasValue ||
                     experiment.RecoveryFinalizedAtUtc.HasValue))
                {
                    return false;
                }

                if (experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.None &&
                    string.IsNullOrWhiteSpace(experiment.RecoveryStatus))
                {
                    return false;
                }

                if (experiment.RecoveryFinalized &&
                    (!experiment.RecoverySucceeded ||
                     experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.Finalized ||
                     !experiment.RecoveryFinalizedAtUtc.HasValue ||
                     experiment.RecoveryRequiresReboot))
                {
                    return false;
                }

                if (experiment.RecoveryAttempted &&
                    !experiment.RecoveryAttemptedAtUtc.HasValue)
                {
                    return false;
                }

                if (experiment.RecoveryRequiresReboot &&
                    (!experiment.RecoverySucceeded ||
                     !experiment.RecoveryAttempted ||
                     experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.RebootRequired))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Failed &&
                    (!experiment.RecoveryAttempted ||
                     !experiment.RecoveryAttemptedAtUtc.HasValue ||
                     experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized ||
                     experiment.RecoverySucceeded ||
                     string.IsNullOrWhiteSpace(experiment.RecoveryStatus)))
                {
                    return false;
                }

                if (experiment.RecoveryFinalizedAtUtc.HasValue &&
                    experiment.RecoveryAttemptedAtUtc.HasValue &&
                    experiment.RecoveryFinalizedAtUtc.Value < experiment.RecoveryAttemptedAtUtc.Value)
                {
                    return false;
                }

                if (experiment.BaselineCompleted)
                {
                    if (experiment.Baseline == null ||
                        !experiment.Baseline.IsComplete ||
                        experiment.Baseline.CompletedRuns < 3 ||
                        experiment.WorkloadDefinition == null ||
                        !experiment.WorkloadDefinition.IsValid ||
                        string.IsNullOrWhiteSpace(experiment.WorkloadFingerprint) ||
                        !string.Equals(
                            experiment.WorkloadFingerprint,
                            experiment.Baseline.WorkloadFingerprint,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetExperimentDirectory()
        {
            Directory.CreateDirectory(ExperimentDirectory);
            return ExperimentDirectory;
        }

        private static string GetPath(string experimentId)
        {
            return Path.Combine(
                ExperimentDirectory,
                experimentId + ".json");
        }

        private static void ValidateId(string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId))
                throw new ArgumentException(
                    "Invalid experiment ID.",
                    nameof(experimentId));

            foreach (char character in experimentId)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    throw new ArgumentException(
                        "Invalid experiment ID.",
                        nameof(experimentId));
                }
            }
        }
    }
}