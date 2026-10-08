using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace BuildCore
{
    public static class OptimizationRestoreService
    {
        public static OptimizationRestoreResult Restore(
            OptimizationTransaction transaction)
        {
            if (transaction == null)
            {
                return Failure(
                    "Invalid optimization transaction.");
            }

            if (!transaction.RestoreAvailable)
            {
                return Failure(
                    "Restore is not available for this transaction.");
            }

            if (!OptimizationTransactionService.ValidateTransaction(
                transaction.TransactionId))
            {
                return Failure(
                    "The optimization transaction failed integrity validation.");
            }

            if (!OptimizationTransactionService.HasRestoreHandler(
                transaction.OptimizationTitle))
            {
                return Failure(
                    "The restore handler for this optimization is no longer available.");
            }

            try
            {
                if (transaction.OptimizationTitle.Equals("Disable adapter power saving", StringComparison.OrdinalIgnoreCase))
                {
                    return NetworkAdapterPowerOptimizationHandler.Restore(transaction.BeforeValue);
                }

                if (RegistryOptimizationHandler.IsSupported(transaction.OptimizationTitle))
                {
                    return RegistryOptimizationHandler.Restore(transaction);

                }

                return transaction.OptimizationTitle switch
                {
                    "CPU idle policy" => PowerOptimizationHandler.RestoreAcPercentage("processor-min-ac", transaction.BeforeValue),
                    "USB selective suspend" => PowerOptimizationHandler.RestoreAcFlag("usb-selective-ac", transaction.BeforeValue),
                    "PCIe link state power management" => PowerOptimizationHandler.RestoreAcFlag("pcie-link-ac", transaction.BeforeValue),
                    "Processor boost policy" => PowerOptimizationHandler.RestoreAcInteger("processor-boost-ac", transaction.BeforeValue),
                    "Performance Power Plan" =>
                        RestorePowerPlan(transaction),

                    "Enable Windows Game Mode" =>
                        RestoreGameMode(transaction),

                    "Prevent sleep during workload" =>
                        PowerOptimizationHandler.RestoreAcTimeout(
                            "standby-timeout-ac",
                            transaction.BeforeValue),

                    "Workload display timeout" =>
                        PowerOptimizationHandler.RestoreAcTimeout(
                            "monitor-timeout-ac",
                            transaction.BeforeValue),

                    _ =>
                        Failure(
                            "No restore handler exists for this optimization.")
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE RESTORE ERROR");

                Debug.WriteLine(
                    ex.ToString());

                return new OptimizationRestoreResult
                {
                    Success = false,
                    Verified = false,
                    Message = "Restore failed.",
                    Error = ex.Message
                };
            }
        }

        private static OptimizationRestoreResult
            RestorePowerPlan(
                OptimizationTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(transaction.BeforeValue))
            {
                return Failure("The original power plan was not recorded.");
            }

            ActivePowerPlanState? previousPlan = null;
            try
            {
                previousPlan = System.Text.Json.JsonSerializer.Deserialize<ActivePowerPlanState>(transaction.BeforeValue);
            }
            catch
            {
                // Legacy transactions may contain only the display name.
            }

            string? schemeGuid = previousPlan?.Guid;
            string previousName = previousPlan?.Name ?? transaction.BeforeValue;

            if (string.IsNullOrWhiteSpace(schemeGuid) &&
                previousName.Equals("Balanced", StringComparison.OrdinalIgnoreCase))
                schemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
            else if (string.IsNullOrWhiteSpace(schemeGuid) &&
                     previousName.Equals("High Performance", StringComparison.OrdinalIgnoreCase))
                schemeGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
            else if (string.IsNullOrWhiteSpace(schemeGuid) &&
                     previousName.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                schemeGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

            if (string.IsNullOrWhiteSpace(schemeGuid))
                return Failure($"BuildCore cannot safely restore the original power plan '{previousName}' because its GUID was not recorded.");

            RunProcess("powercfg", $"/setactive {schemeGuid}");

            string verificationState = WindowsSystemService.CaptureActivePowerPlanState();
            ActivePowerPlanState? verifiedPlan =
                System.Text.Json.JsonSerializer.Deserialize<ActivePowerPlanState>(verificationState);

            bool verified = verifiedPlan != null &&
                verifiedPlan.Guid.Equals(schemeGuid, StringComparison.OrdinalIgnoreCase);

            if (verified)
            {
                return new OptimizationRestoreResult
                {
                    Success = true,
                    Verified = true,
                    Message = $"Power plan restored to {verifiedPlan!.Name} and verified."
                };
            }

            return new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message = $"BuildCore attempted to restore {previousName}, but verification failed."
            };
        }

        private static OptimizationRestoreResult
            RestoreGameMode(
                OptimizationTransaction transaction)
        {
            if (transaction.BeforeValue.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                return Failure("The original Game Mode registry state could not be safely captured.");

            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
            if (key == null)
                return Failure("BuildCore could not access the Windows Game Mode registry settings.");

            if (transaction.BeforeValue.Equals("missing", StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue("AutoGameModeEnabled", false);
            }
            else if (transaction.BeforeValue.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
                     transaction.BeforeValue.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(
                    "AutoGameModeEnabled",
                    transaction.BeforeValue.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    RegistryValueKind.DWord);
            }
            else
            {
                return Failure("The original Game Mode registry state is invalid.");
            }

            key.Flush();

            string restoredState = WindowsSystemService.CaptureGameModeState();
            bool verified = restoredState.Equals(transaction.BeforeValue, StringComparison.OrdinalIgnoreCase);

            return new OptimizationRestoreResult
            {
                Success = verified,
                Verified = verified,
                Message = verified
                    ? $"Windows Game Mode restored to {transaction.BeforeValue} and verified."
                    : "Game Mode was changed, but BuildCore could not verify the restored registry state.",
                Error = verified ? null : "Game Mode restore verification failed."
            };
        }

        private static string RunProcess(
            string fileName,
            string arguments)
        {
            using var process =
                new Process();

            process.StartInfo =
                new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            process.Start();

            string output =
                process.StandardOutput.ReadToEnd();

            string error =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"Process exited with code {process.ExitCode}."
                        : error.Trim());
            }

            return output;
        }

        private static OptimizationRestoreResult
            Failure(string message)
        {
            return new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = message
            };
        }
    }

    public class OptimizationRestoreResult
    {
        public bool Success { get; set; }

        public bool Verified { get; set; }

        public string Message { get; set; } = "";

        public string? Error { get; set; }
    }
}