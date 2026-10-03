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

        private static readonly object StorageLock = new object();

        public static void Save(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            ValidateId(experiment.ExperimentId);

            lock (StorageLock)
            {
                SaveCore(experiment);
            }
        }

        private static void SaveCore(
            RebootOptimizationExperimentState experiment)
        {
            Directory.CreateDirectory(ExperimentDirectory);

            experiment.UpdatedAt = DateTime.Now;

            if (experiment.IsPendingReboot &&
                !experiment.BaselineBootTimeUtc.HasValue)
            {
                experiment.BaselineBootTimeUtc =
                    RebootOptimizationExperimentValidationService.GetCurrentBootTimeUtc();
            }

            string path = GetPath(experiment.ExperimentId);
            string tempPath = path + ".tmp";
            string json = JsonSerializer.Serialize(experiment, JsonOptions);

            try
            {
                // Write the complete document before replacing the live state file.
                // This prevents a partial JSON document from becoming the persisted
                // experiment if the process is interrupted during the write.
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                // Keep the last known-good document as a recovery copy.
                if (File.Exists(path))
                {
                    string backupPath = path + ".bak";
                    string backupTempPath = backupPath + ".tmp";

                    try
                    {
                        File.Copy(path, backupTempPath, overwrite: true);
                        File.Move(
                            backupTempPath,
                            backupPath,
                            overwrite: true);
                    }
                    catch
                    {
                        try
                        {
                            if (File.Exists(backupTempPath))
                                File.Delete(backupTempPath);
                        }
                        catch
                        {
                            // Preserve the original backup failure.
                        }

                        throw;
                    }
                }

                File.Move(tempPath, path, overwrite: true);

                // The primary file is now the newest known-good state.
                // Keep the backup as the previous known-good state for recovery.
                // It is intentionally not deleted here.

                // Confirm the persisted document is readable and represents
                // the same experiment before reporting a successful save.
                RebootOptimizationExperimentState? persisted =
                    JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                        File.ReadAllText(path),
                        JsonOptions);

                if (persisted == null ||
                    !string.Equals(
                        persisted.ExperimentId,
                        experiment.ExperimentId,
                        StringComparison.Ordinal) ||
                    !ValidateLoadedExperiment(persisted))
                {
                    string backupPath = path + ".bak";

                    try
                    {
                        if (File.Exists(backupPath))
                        {
                            File.Copy(backupPath, path, overwrite: true);
                        }
                        else if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                        // Preserve the validation failure below. The backup remains
                        // available for the next load attempt when present.
                    }

                    throw new IOException(
                        "Persisted reboot experiment failed post-write validation.");
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Preserve the original save failure.
                }

                throw;
            }
        }

        public static RebootOptimizationExperimentState? Load(
            string experimentId)
        {
            lock (StorageLock)
            {
                return LoadCore(experimentId);
            }
        }

        private static RebootOptimizationExperimentState? LoadCore(
            string experimentId)
        {
            try
            {
                ValidateId(experimentId);

                if (!IsSafeExperimentId(experimentId))
                    return null;

                string path = GetPath(experimentId);

                if (!File.Exists(path))
                    return TryLoadBackup(experimentId);

                string json = File.ReadAllText(path);

                RebootOptimizationExperimentState? experiment =
                    JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                        json,
                        JsonOptions);

                if (experiment == null ||
                    !string.Equals(
                        experiment.ExperimentId,
                        experimentId,
                        StringComparison.Ordinal) ||
                    !ValidateLoadedExperiment(experiment))
                {
                    experiment = TryLoadBackup(experimentId);
                }

                if (experiment != null && MigrateLoadedExperiment(experiment))
                    Save(experiment);

                return experiment;
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

                    string experimentId =
                        Path.GetFileNameWithoutExtension(file);

                    if (string.IsNullOrWhiteSpace(experimentId) ||
                        !IsSafeExperimentId(experimentId))
                    {
                        continue;
                    }

                    if (experiment == null ||
                        !string.Equals(
                            experiment.ExperimentId,
                            experimentId,
                            StringComparison.Ordinal))
                    {
                        RebootOptimizationExperimentState? backup =
                            TryLoadBackup(experimentId);

                        if (backup == null)
                            continue;

                        experiment = backup;
                    }

                    if (MigrateLoadedExperiment(experiment))
                    {
                        Save(experiment);
                    }

                    if (!ValidateLoadedExperiment(experiment))
                    {
                        RebootOptimizationExperimentState? backup =
                            TryLoadBackup(experiment.ExperimentId);

                        if (backup == null ||
                            !ValidateLoadedExperiment(backup))
                        {
                            continue;
                        }

                        experiment = backup;
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

        private static bool IsSafeExperimentId(
            string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId) ||
                experimentId.Length > 128)
            {
                return false;
            }

            foreach (char character in experimentId)
            {
                if (!(char.IsLetterOrDigit(character) ||
                      character == '-' ||
                      character == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RestorePrimaryFromBackup(
            string experimentId)
        {
            try
            {
                string primaryPath = GetPath(experimentId);
                string backupPath = primaryPath + ".bak";
                string tempPath = primaryPath + ".recovery.tmp";

                if (!File.Exists(backupPath))
                    return;

                File.Copy(backupPath, tempPath, overwrite: true);
                File.Move(tempPath, primaryPath, overwrite: true);

                RebootOptimizationExperimentState? restored =
                    JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                        File.ReadAllText(primaryPath),
                        JsonOptions);

                if (restored == null ||
                    !string.Equals(
                        restored.ExperimentId,
                        experimentId,
                        StringComparison.Ordinal) ||
                    !ValidateLoadedExperiment(restored))
                {
                    throw new IOException(
                        "Recovered reboot experiment failed post-restore validation.");
                }
            }
            catch
            {
                try
                {
                    string tempPath = GetPath(experimentId) + ".recovery.tmp";
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Recovery remains available through the backup file.
                }
            }
        }

        private static RebootOptimizationExperimentState? TryLoadBackup(
            string experimentId)
        {
            try
            {
                string backupPath = GetPath(experimentId) + ".bak";

                if (!File.Exists(backupPath))
                    return null;

                string json = File.ReadAllText(backupPath);

                RebootOptimizationExperimentState? experiment =
                    JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                        json,
                        JsonOptions);

                if (experiment == null ||
                    !string.Equals(
                        experiment.ExperimentId,
                        experimentId,
                        StringComparison.Ordinal) ||
                    !ValidateLoadedExperiment(experiment))
                {
                    return null;
                }

                return experiment;
            }
            catch
            {
                return null;
            }
        }

        private static bool MigrateLoadedExperiment(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment.SchemaVersion == 0)
            {
                // Version 0 predates explicit schema versioning. Only adopt the
                // current contract when no recovery state is present. Recovery
                // metadata cannot be safely interpreted without its schema.
                if (experiment.RecoveryAvailable ||
                    !string.IsNullOrWhiteSpace(experiment.RecoveryHandlerId) ||
                    experiment.RecoveryHandlerVersion != 0 ||
                    experiment.RecoveryPhase !=
                        RebootOptimizationExperimentRecoveryPhase.None ||
                    experiment.RecoveryAttempted ||
                    experiment.RecoverySucceeded ||
                    experiment.RecoveryAttemptedAtUtc.HasValue ||
                    experiment.RecoveryRequiresReboot ||
                    experiment.RecoveryFinalized ||
                    experiment.RecoveryFinalizedAtUtc.HasValue ||
                    experiment.RecoveryFinalizationCheckedAtUtc.HasValue)
                {
                    return false;
                }

                experiment.SchemaVersion =
                    RebootOptimizationExperimentState.CurrentSchemaVersion;
                return true;
            }

            return false;
        }

        private static bool ValidateLoadedExperiment(
            RebootOptimizationExperimentState experiment)
        {
            try
            {
                if (experiment.SchemaVersion <= 0 ||
                    experiment.SchemaVersion > RebootOptimizationExperimentState.CurrentSchemaVersion)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(experiment.ExperimentId) ||
                    experiment.CreatedAt == default ||
                    experiment.UpdatedAt == default ||
                    experiment.UpdatedAt < experiment.CreatedAt ||
                    string.IsNullOrWhiteSpace(experiment.OptimizationTitle))
                {
                    return false;
                }

                if (!experiment.RecoveryAvailable &&
                    (!string.IsNullOrWhiteSpace(experiment.RecoveryHandlerId) ||
                     experiment.RecoveryHandlerVersion != 0))
                {
                    return false;
                }

                if (experiment.RecoveryAvailable &&
                    RebootOptimizationRecoveryHandlerRegistry.Find(experiment) == null)
                {
                    return false;
                }

                if (!Enum.IsDefined(
                    typeof(RebootOptimizationExperimentRecoveryPhase),
                    experiment.RecoveryPhase))
                {
                    return false;
                }

                if (experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.None &&
                    (!experiment.RecoveryAvailable ||
                     string.IsNullOrWhiteSpace(experiment.RecoveryHandlerId) ||
                     experiment.RecoveryHandlerVersion <= 0))
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

                if (experiment.RecoveryFinalizationCheckedAtUtc.HasValue &&
                    (!experiment.RecoveryAttemptedAtUtc.HasValue ||
                     experiment.RecoveryFinalizationCheckedAtUtc.Value <
                        experiment.RecoveryAttemptedAtUtc.Value))
                {
                    return false;
                }

                if (experiment.RecoveryFinalizedAtUtc.HasValue &&
                    (!experiment.RecoveryFinalizationCheckedAtUtc.HasValue ||
                     !experiment.RecoveryAttemptedAtUtc.HasValue ||
                     experiment.RecoveryFinalizedAtUtc.Value <
                        experiment.RecoveryAttemptedAtUtc.Value))
                {
                    return false;
                }

                if (experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.None &&
                    string.IsNullOrWhiteSpace(experiment.RecoveryStatus))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Available &&
                    (!experiment.RecoveryAvailable ||
                     experiment.RecoveryAttempted ||
                     experiment.RecoverySucceeded ||
                     experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized))
                {
                    return false;
                }

                if (experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Failed &&
                    (!experiment.RecoveryAttempted ||
                     !experiment.RecoveryAttemptedAtUtc.HasValue ||
                     experiment.RecoveryRequiresReboot ||
                     experiment.RecoveryFinalized))
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

                if (experiment.RecoveryFinalized &&
                    (!experiment.RecoverySucceeded ||
                     experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.Finalized ||
                     !experiment.RecoveryFinalizedAtUtc.HasValue ||
                     experiment.RecoveryRequiresReboot))
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

        public static RebootOptimizationExperimentState? GetPending()
        {
            return GetExperiments()
                .FirstOrDefault(e => e.IsPendingReboot);
        }

        public static bool CleanupTemporaryFiles()
        {
            try
            {
                if (!Directory.Exists(ExperimentDirectory))
                    return true;

                foreach (string file in Directory.GetFiles(
                    ExperimentDirectory,
                    "*.tmp"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // A temporary file that is in use can be removed later.
                    }
                }

                foreach (string file in Directory.GetFiles(
                    ExperimentDirectory,
                    "*.recovery.tmp"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // A temporary recovery file that is in use can be removed later.
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
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

        public static RebootOptimizationExperimentStorageHealth GetHealth(
            string experimentId)
        {
            if (!IsSafeExperimentId(experimentId))
            {
                return new RebootOptimizationExperimentStorageHealth
                {
                    ExperimentId = experimentId,
                    IsValidId = false
                };
            }

            lock (StorageLock)
            {
                string primaryPath = GetPath(experimentId);
                string backupPath = primaryPath + ".bak";

                RebootOptimizationExperimentState? primary =
                    TryLoadValidatedFile(primaryPath, experimentId);

                RebootOptimizationExperimentState? backup =
                    TryLoadValidatedFile(backupPath, experimentId);

                return new RebootOptimizationExperimentStorageHealth
                {
                    ExperimentId = experimentId,
                    IsValidId = true,
                    PrimaryExists = File.Exists(primaryPath),
                    PrimaryValid = primary != null,
                    BackupExists = File.Exists(backupPath),
                    BackupValid = backup != null,
                    IsHealthy = primary != null,
                    CanRecoverFromBackup = primary == null && backup != null
                };
            }
        }

        private static RebootOptimizationExperimentState? TryLoadValidatedFile(
            string path,
            string experimentId)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                RebootOptimizationExperimentState? experiment =
                    JsonSerializer.Deserialize<RebootOptimizationExperimentState>(
                        File.ReadAllText(path),
                        JsonOptions);

                if (experiment == null ||
                    !string.Equals(
                        experiment.ExperimentId,
                        experimentId,
                        StringComparison.Ordinal) ||
                    !ValidateLoadedExperiment(experiment))
                {
                    return null;
                }

                return experiment;
            }
            catch
            {
                return null;
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

                if (!experiment.RecoveryAvailable &&
                    (!string.IsNullOrWhiteSpace(experiment.RecoveryHandlerId) ||
                     experiment.RecoveryHandlerVersion != 0))
                {
                    return false;
                }

                if (experiment.RecoveryAvailable &&
                    !RebootOptimizationExperimentRecoveryService.HasRecoveryHandler(
                        experiment))
                {
                    return false;
                }

                if (experiment.RecoveryAvailable)
                {
                    IRebootOptimizationRecoveryHandler? handler =
                        RebootOptimizationRecoveryHandlerRegistry.Find(
                            experiment);

                    if (handler == null)
                    {
                        return false;
                    }
                }

                if (!Enum.IsDefined(
                    typeof(RebootOptimizationExperimentRecoveryPhase),
                    experiment.RecoveryPhase))
                {
                    return false;
                }

                if (experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.None && !experiment.RecoveryAvailable) return false;

                if (experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.None && (string.IsNullOrWhiteSpace(experiment.RecoveryHandlerId) || experiment.RecoveryHandlerVersion <= 0)) return false;

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
                     string.IsNullOrWhiteSpace(experiment.RecoveryStatus)))
                {
                    return false;
                }

                if (experiment.RecoveryFinalizationCheckedAtUtc.HasValue &&
                    !experiment.RecoveryAttemptedAtUtc.HasValue)
                {
                    return false;
                }

                if (experiment.RecoveryFinalizationCheckedAtUtc.HasValue &&
                    experiment.RecoveryAttemptedAtUtc.HasValue &&
                    experiment.RecoveryFinalizationCheckedAtUtc.Value <
                    experiment.RecoveryAttemptedAtUtc.Value)
                {
                    return false;
                }

                if (experiment.RecoveryFinalized &&
                    !experiment.RecoveryFinalizationCheckedAtUtc.HasValue)
                {
                    return false;
                }

                if (experiment.RecoveryFinalizedAtUtc.HasValue &&
                    !experiment.RecoveryFinalizationCheckedAtUtc.HasValue)
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