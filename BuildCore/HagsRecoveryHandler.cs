using Microsoft.Win32;
using System;

namespace BuildCore
{
    public sealed class HagsRecoveryHandler : IRebootOptimizationRecoveryHandler
    {
        public const string Title = "Hardware-Accelerated GPU Scheduling";
        private const string GraphicsDriversPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string HagsValueName = "HwSchMode";

        public string OptimizationTitle => Title;

        public void CaptureOriginalState(RebootOptimizationExperimentState experiment)
        {
            if (experiment == null) throw new ArgumentNullException(nameof(experiment));
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath, false);
            object? value = key?.GetValue(HagsValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (value != null && value is not int)
            {
                throw new InvalidOperationException(
                    "The existing HAGS registry value is not a DWORD. " +
                    "BuildCore will not overwrite or delete an unsupported registry value.");
            }

            experiment.OriginalHagsValueExists = value is int;
            experiment.OriginalHagsMode = value is int mode ? mode : null;
            experiment.RecoveryPhase = RebootOptimizationExperimentRecoveryPhase.Available;
        }

        public bool CanRollback(RebootOptimizationExperimentState experiment)
        {
            return experiment != null && experiment.RecoveryAvailable &&
                !experiment.RecoveryAttempted &&
                experiment.RecoveryPhase == RebootOptimizationExperimentRecoveryPhase.Available &&
                string.Equals(experiment.OptimizationTitle, Title, StringComparison.Ordinal);
        }

        public OptimizationApplyResult Rollback(RebootOptimizationExperimentState experiment)
        {
            if (!CanRollback(experiment))
                return Failure(experiment, "Rollback unavailable.");

            try
            {
                if (string.IsNullOrWhiteSpace(experiment.SnapshotId) || !experiment.SnapshotCreated ||
                    !SnapshotService.ValidateSnapshot(experiment.SnapshotId))
                {
                    RecordFailure(experiment, "Rollback blocked because the experiment snapshot is missing or invalid.");
                    return Failure(experiment, "Invalid snapshot.");
                }

                using RegistryKey? key = Registry.LocalMachine.CreateSubKey(GraphicsDriversPath);
                if (key == null)
                {
                    RecordFailure(experiment, "BuildCore could not access the HAGS registry setting.");
                    return Failure(experiment, "Registry access failed.");
                }

                if (experiment.OriginalHagsValueExists && experiment.OriginalHagsMode.HasValue)
                    key.SetValue(HagsValueName, experiment.OriginalHagsMode.Value, RegistryValueKind.DWord);
                else
                    key.DeleteValue(HagsValueName, false);

                key.Flush();
                bool verified = VerifyRestoredState(experiment);
                experiment.RecoveryAttempted = true;
                experiment.RecoveryAttemptedAtUtc = DateTime.UtcNow;
                experiment.RecoverySucceeded = verified;
                experiment.RecoveryStatus = verified
                    ? "Original HAGS state restored successfully. Windows restart may be required for the change to take effect."
                    : "Rollback was attempted, but the restored HAGS state could not be verified.";

                if (verified)
                {
                    experiment.OptimizationChangePending = false;
                    experiment.RecoveryRequiresReboot = true;
                    experiment.RecoveryPhase = RebootOptimizationExperimentRecoveryPhase.RebootRequired;
                }

                RebootOptimizationExperimentStorageService.Save(experiment);
                return new OptimizationApplyResult
                {
                    Success = verified,
                    Verified = verified,
                    Message = experiment.RecoveryStatus,
                    Error = verified ? "" : "Rollback verification failed."
                };
            }
            catch (Exception ex)
            {
                RecordFailure(experiment, $"Rollback failed: {ex.Message}");
                return Failure(experiment, ex.ToString());
            }
        }

        public bool VerifyRestoredState(RebootOptimizationExperimentState experiment)
        {
            if (experiment == null || !experiment.RecoveryAttempted) return false;
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(GraphicsDriversPath, false);
            if (key == null) return !experiment.OriginalHagsValueExists;
            object? value = key.GetValue(HagsValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (!experiment.OriginalHagsValueExists) return value == null;
            return value is int mode && experiment.OriginalHagsMode.HasValue && mode == experiment.OriginalHagsMode.Value;
        }

        private static OptimizationApplyResult Failure(RebootOptimizationExperimentState experiment, string error)
        {
            return new OptimizationApplyResult { Success = false, Verified = false, Message = experiment?.RecoveryStatus ?? "", Error = error };
        }

        private static void RecordFailure(RebootOptimizationExperimentState experiment, string message)
        {
            experiment.RecoveryAttempted = true;
            experiment.RecoveryAttemptedAtUtc = DateTime.UtcNow;
            experiment.RecoverySucceeded = false;
            experiment.RecoveryPhase = RebootOptimizationExperimentRecoveryPhase.Failed;
            experiment.RecoveryStatus = message;
            RebootOptimizationExperimentStorageService.Save(experiment);
        }
    }
}