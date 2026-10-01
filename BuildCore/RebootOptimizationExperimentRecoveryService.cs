using System;
using System.Management;

namespace BuildCore
{
    public static class RebootOptimizationExperimentRecoveryService
    {
        public static bool HasRecoveryHandler(
            RebootOptimizationExperimentState experiment)
        {
            return RebootOptimizationRecoveryHandlerRegistry.Find(experiment) != null;
        }

        public static void CaptureOriginalState(
            RebootOptimizationExperimentState experiment)
        {
            if (experiment == null)
                throw new ArgumentNullException(nameof(experiment));

            IRebootOptimizationRecoveryHandler? handler =
                RebootOptimizationRecoveryHandlerRegistry.Find(experiment);

            if (handler == null)
                throw new InvalidOperationException(
                    $"No recovery handler is registered for '{experiment.OptimizationTitle}'.");

            handler.CaptureOriginalState(experiment);
        }

        public static bool CanRollback(RebootOptimizationExperimentState experiment)
        {
            return RebootOptimizationRecoveryHandlerRegistry.Find(experiment)?.CanRollback(experiment) == true;
        }

        public static bool FinalizeRecoveryAfterReboot(RebootOptimizationExperimentState experiment)
        {
            if (experiment == null || !experiment.RecoverySucceeded ||
                !experiment.RecoveryRequiresReboot || experiment.RecoveryFinalized ||
                !experiment.RecoveryAttemptedAtUtc.HasValue)
                return false;

            try
            {
                experiment.RecoveryFinalizationCheckedAtUtc = DateTime.UtcNow;
                RebootOptimizationExperimentStorageService.Save(experiment);

                DateTime bootTimeUtc = GetCurrentBootTimeUtc();
                if (bootTimeUtc <= experiment.RecoveryAttemptedAtUtc.Value)
                    return false;

                IRebootOptimizationRecoveryHandler? handler =
                    RebootOptimizationRecoveryHandlerRegistry.Find(experiment);

                if (handler == null || !handler.VerifyRestoredState(experiment))
                {
                    RecordFinalizationFailure(
                        experiment,
                        "Windows restarted, but BuildCore could not verify the original recovery state after recovery.");
                    return false;
                }

                experiment.RecoveryFinalized = true;
                experiment.RecoveryFinalizedAtUtc = DateTime.UtcNow;
                experiment.RecoveryRequiresReboot = false;
                experiment.RecoveryPhase =
                    RebootOptimizationExperimentRecoveryPhase.Finalized;
                experiment.RecoveryStatus =
                    "Original recovery state restored and verified after Windows restart.";
                RebootOptimizationExperimentStorageService.Save(experiment);
                return true;
            }
            catch (Exception ex)
            {
                RecordFinalizationFailure(experiment, $"Recovery finalization failed: {ex.Message}");
                return false;
            }
        }

        public static bool VerifyRestoredState(RebootOptimizationExperimentState experiment)
        {
            return RebootOptimizationRecoveryHandlerRegistry.Find(experiment)
                ?.VerifyRestoredState(experiment) == true;
        }

        public static bool RetryFinalizationAfterFailure(RebootOptimizationExperimentState experiment)
        {
            if (experiment == null ||
                experiment.RecoveryPhase != RebootOptimizationExperimentRecoveryPhase.Failed ||
                !experiment.RecoverySucceeded ||
                !experiment.RecoveryAttemptedAtUtc.HasValue)
                return false;

            try
            {
                experiment.RecoveryFinalizationCheckedAtUtc = DateTime.UtcNow;
                RebootOptimizationExperimentStorageService.Save(experiment);

                DateTime bootTimeUtc = GetCurrentBootTimeUtc();
                if (bootTimeUtc <= experiment.RecoveryAttemptedAtUtc.Value)
                    return false;

                IRebootOptimizationRecoveryHandler? handler =
                    RebootOptimizationRecoveryHandlerRegistry.Find(experiment);

                if (handler == null || !handler.VerifyRestoredState(experiment))
                    return false;

                experiment.RecoveryFinalized = true;
                experiment.RecoveryFinalizedAtUtc = DateTime.UtcNow;
                experiment.RecoveryRequiresReboot = false;
                experiment.RecoveryPhase =
                    RebootOptimizationExperimentRecoveryPhase.Finalized;
                experiment.RecoveryStatus =
                    "Original recovery state restored and verified after recovery re-verification.";
                RebootOptimizationExperimentStorageService.Save(experiment);
                return true;
            }
            catch (Exception ex)
            {
                experiment.RecoveryStatus =
                    $"Recovery re-verification failed: {ex.Message}";
                RebootOptimizationExperimentStorageService.Save(experiment);
                return false;
            }
        }

        public static OptimizationApplyResult Rollback(
            RebootOptimizationExperimentState experiment)
        {
            IRebootOptimizationRecoveryHandler? handler =
                RebootOptimizationRecoveryHandlerRegistry.Find(experiment);

            if (handler == null)
            {
                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = "This experiment does not have an available rollback handler.",
                    Error = "Rollback unavailable."
                };
            }

            return handler.Rollback(experiment);
        }

        private static DateTime GetCurrentBootTimeUtc()
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LastBootUpTime FROM Win32_OperatingSystem");

            foreach (ManagementObject item in searcher.Get())
            {
                string? value = item["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return ManagementDateTimeConverter
                        .ToDateTime(value)
                        .ToUniversalTime();
                }
            }

            throw new InvalidOperationException(
                "Could not determine Windows boot time.");
        }

        private static void RecordFinalizationFailure(
            RebootOptimizationExperimentState experiment,
            string message)
        {
            experiment.RecoveryPhase =
                RebootOptimizationExperimentRecoveryPhase.Failed;
            experiment.RecoveryRequiresReboot = false;
            experiment.RecoveryFinalized = false;
            experiment.RecoveryStatus = message;
            RebootOptimizationExperimentStorageService.Save(experiment);
        }
    }
}