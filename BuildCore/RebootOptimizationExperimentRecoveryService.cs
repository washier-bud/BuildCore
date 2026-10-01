using Microsoft.Win32;
using System;

namespace BuildCore
{
    public static class RebootOptimizationExperimentRecoveryService
    {
        private const string HagsTitle =
            "Hardware-Accelerated GPU Scheduling";

        private const string GraphicsDriversPath =
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

        private const string HagsValueName = "HwSchMode";

        public static void CaptureOriginalHagsState(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            using RegistryKey? key =
                Registry.LocalMachine.OpenSubKey(
                    GraphicsDriversPath,
                    writable: false);

            if (key == null)
            {
                experiment.OriginalHagsValueExists = false;
                experiment.OriginalHagsMode = null;
                return;
            }

            object? value =
                key.GetValue(
                    HagsValueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (value is int mode)
            {
                experiment.OriginalHagsValueExists = true;
                experiment.OriginalHagsMode = mode;
            }
            else
            {
                experiment.OriginalHagsValueExists = false;
                experiment.OriginalHagsMode = null;
            }
        }

        public static bool CanRollback(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                return false;

            return experiment.RecoveryAvailable &&
                   !experiment.RecoveryAttempted &&
                   string.Equals(
                       experiment.OptimizationTitle,
                       HagsTitle,
                       StringComparison.Ordinal);
        }

        public static bool VerifyRestoredHagsState(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null || !experiment.RecoveryAttempted)
                return false;

            return VerifyHagsState(experiment);
        }

        public static OptimizationApplyResult Rollback(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            if (!CanRollback(experiment))
            {
                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = "This experiment does not have an available rollback handler.",
                    Error = "Rollback unavailable."
                };
            }

            try
            {
                if (string.IsNullOrWhiteSpace(experiment.SnapshotId) ||
                    !experiment.SnapshotCreated ||
                    !SnapshotService.ValidateSnapshot(experiment.SnapshotId))
                {
                    RecordFailure(
                        experiment,
                        "Rollback blocked because the experiment snapshot is missing or invalid.");

                    return new OptimizationApplyResult
                    {
                        Success = false,
                        Verified = false,
                        Message = experiment.RecoveryStatus,
                        Error = "Invalid snapshot."
                    };
                }

                using RegistryKey? key =
                    Registry.LocalMachine.CreateSubKey(
                        GraphicsDriversPath);

                if (key == null)
                {
                    RecordFailure(
                        experiment,
                        "BuildCore could not access the HAGS registry setting.");
                    return new OptimizationApplyResult
                    {
                        Success = false,
                        Verified = false,
                        Message = experiment.RecoveryStatus,
                        Error = "Registry access failed."
                    };
                }

                if (experiment.OriginalHagsValueExists &&
                    experiment.OriginalHagsMode.HasValue)
                {
                    key.SetValue(
                        HagsValueName,
                        experiment.OriginalHagsMode.Value,
                        RegistryValueKind.DWord);
                }
                else
                {
                    key.DeleteValue(
                        HagsValueName,
                        throwOnMissingValue: false);
                }

                key.Flush();

                bool verified =
                    VerifyHagsState(experiment);

                experiment.RecoveryAttempted = true;
                experiment.RecoveryAttemptedAtUtc = DateTime.UtcNow;
                experiment.RecoverySucceeded = verified;
                experiment.RecoveryStatus =
                    verified
                        ? "Original HAGS state restored successfully. Windows restart may be required for the change to take effect."
                        : "Rollback was attempted, but the restored HAGS state could not be verified.";

                if (verified)
                    experiment.OptimizationChangePending = false;

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
                RecordFailure(
                    experiment,
                    $"Rollback failed: {ex.Message}");

                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = experiment.RecoveryStatus,
                    Error = ex.ToString()
                };
            }
        }

        private static bool VerifyHagsState(
            RebootOptimizationExperimentState experiment)
        {
            using RegistryKey? key =
                Registry.LocalMachine.OpenSubKey(
                    GraphicsDriversPath,
                    writable: false);

            if (key == null)
                return !experiment.OriginalHagsValueExists;

            object? value =
                key.GetValue(
                    HagsValueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (!experiment.OriginalHagsValueExists)
                return value == null;

            return value is int mode &&
                   experiment.OriginalHagsMode.HasValue &&
                   mode == experiment.OriginalHagsMode.Value;
        }

        private static void RecordFailure(
            RebootOptimizationExperimentState experiment,
            string message)
        {
            experiment.RecoveryAttempted = true;
            experiment.RecoveryAttemptedAtUtc = DateTime.UtcNow;
            experiment.RecoverySucceeded = false;
            experiment.RecoveryStatus = message;
            RebootOptimizationExperimentStorageService.Save(experiment);
        }
    }
}