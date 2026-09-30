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

            try
            {
                return transaction.OptimizationTitle switch
                {
                    "Performance Power Plan" =>
                        RestorePowerPlan(transaction),

                    "Enable Windows Game Mode" =>
                        RestoreGameMode(transaction),

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
            string previousPlan =
                transaction.BeforeValue;

            if (string.IsNullOrWhiteSpace(
                previousPlan))
            {
                return Failure(
                    "The original power plan was not recorded.");
            }

            string schemeArgument;

            if (previousPlan.Equals(
                "Balanced",
                StringComparison.OrdinalIgnoreCase))
            {
                schemeArgument =
                    "SCHEME_BALANCED";
            }
            else if (previousPlan.Equals(
                "High Performance",
                StringComparison.OrdinalIgnoreCase))
            {
                schemeArgument =
                    "SCHEME_MIN";
            }
            else if (previousPlan.Equals(
                "Ultimate Performance",
                StringComparison.OrdinalIgnoreCase))
            {
                schemeArgument =
                    "SCHEME_MAX";
            }
            else
            {
                return Failure(
                    $"BuildCore does not yet know how to restore the power plan '{previousPlan}'.");
            }

            RunProcess(
                "powercfg",
                $"/setactive {schemeArgument}");

            WindowsSystemData verification =
                WindowsSystemService.Scan();

            bool verified =
                verification.PowerPlan.Equals(
                    previousPlan,
                    StringComparison.OrdinalIgnoreCase);

            if (verified)
            {
                return new OptimizationRestoreResult
                {
                    Success = true,
                    Verified = true,
                    Message =
                        $"Power plan restored to {previousPlan} and verified."
                };
            }

            return new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message =
                    $"BuildCore attempted to restore {previousPlan}, but verification failed."
            };
        }

        private static OptimizationRestoreResult
            RestoreGameMode(
                OptimizationTransaction transaction)
        {
            bool shouldBeEnabled =
                transaction.BeforeValue.Equals(
                    "Enabled",
                    StringComparison.OrdinalIgnoreCase);

            using RegistryKey? key =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\GameBar");

            if (key == null)
            {
                return Failure(
                    "BuildCore could not access the Windows Game Mode registry settings.");
            }

            key.SetValue(
                "AutoGameModeEnabled",
                shouldBeEnabled ? 1 : 0,
                RegistryValueKind.DWord);

            key.Flush();

            WindowsSystemData verification =
                WindowsSystemService.Scan();

            if (verification.GameModeEnabled ==
                shouldBeEnabled)
            {
                return new OptimizationRestoreResult
                {
                    Success = true,
                    Verified = true,
                    Message =
                        $"Windows Game Mode restored to {(shouldBeEnabled ? "Enabled" : "Disabled")} and verified."
                };
            }

            return new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message =
                    "Game Mode was changed, but BuildCore could not verify the restored state."
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