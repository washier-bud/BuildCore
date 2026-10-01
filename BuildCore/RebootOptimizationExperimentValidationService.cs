using System;
using System.Diagnostics;
using System.Management;

namespace BuildCore
{
    public static class RebootOptimizationExperimentValidationService
    {
        public static DateTime? GetCurrentBootTimeUtc()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT LastBootUpTime FROM Win32_OperatingSystem");

                foreach (ManagementObject item in searcher.Get())
                {
                    string? value = item["LastBootUpTime"]?.ToString();

                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    return ManagementDateTimeConverter
                        .ToDateTime(value)
                        .ToUniversalTime();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE BOOT TIME ERROR");
                Debug.WriteLine(ex.ToString());
            }

            return null;
        }

        public static bool ValidateAfterReboot(
            RebootOptimizationExperimentState experiment,
            out string message)
        {
            if (experiment == null)
            {
                message = "Invalid reboot experiment.";
                return false;
            }

            if (!experiment.IsPendingReboot)
            {
                message = "The experiment is not waiting for a reboot.";
                return false;
            }

            DateTime? currentBootTimeUtc = GetCurrentBootTimeUtc();

            if (!currentBootTimeUtc.HasValue)
            {
                message =
                    "BuildCore could not determine the current Windows boot time.";
                return false;
            }

            experiment.AfterRebootBootTimeUtc =
                currentBootTimeUtc.Value;

            if (!experiment.BaselineBootTimeUtc.HasValue)
            {
                message =
                    "The experiment does not contain a baseline boot time.";
                return false;
            }

            experiment.RebootDetected =
                currentBootTimeUtc.Value >
                experiment.BaselineBootTimeUtc.Value;

            if (!experiment.RebootDetected)
            {
                message =
                    "A new Windows boot has not been detected yet.";
                return false;
            }

            if (experiment.WorkloadDefinition == null ||
                !experiment.WorkloadDefinition.IsValid)
            {
                message =
                    "The saved workload definition is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                experiment.WorkloadFingerprint))
            {
                message =
                    "The saved workload fingerprint is missing.";
                return false;
            }

            string currentPath =
                experiment.WorkloadDefinition.TargetProcessPath ?? "";

            if (experiment.WorkloadDefinition.RequiresInteractiveWorkload &&
                !string.Equals(
                    currentPath,
                    experiment.ExpectedTargetProcessPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                message =
                    "The saved target process path does not match the expected process identity.";
                return false;
            }

            WorkloadEnvironmentSnapshot currentEnvironment =
                WorkloadEnvironmentSnapshot.Capture();

            if (currentEnvironment == null)
            {
                message =
                    "BuildCore could not capture the post-reboot environment.";
                return false;
            }

            if (!string.Equals(
                currentEnvironment.WindowsBuild,
                experiment.BaselineEnvironment?.WindowsBuild,
                StringComparison.Ordinal))
            {
                message =
                    "The Windows build changed across the reboot. " +
                    "The experiment must be treated as inconclusive.";
                return false;
            }

            experiment.AfterRebootValidationPassed = true;
            experiment.Phase =
                RebootOptimizationExperimentPhase.AfterRebootValidation;
            experiment.Status =
                "Reboot validated. Ready for after-workload benchmark.";
            experiment.UpdatedAt = DateTime.Now;

            message =
                "Windows reboot detected and the saved experiment state passed validation.";

            return true;
        }
    }
}
