using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace BuildCore
{
    public class OptimizationApplyResult
    {
        public bool Success { get; set; }

        public bool Verified { get; set; }

        public string Message { get; set; } =
            "";

        public string? Error { get; set; }
    }

    public static class OptimizationApplyService
    {
        public static OptimizationApplyResult Apply(
            OptimizationRecommendation recommendation)
        {
            if (recommendation == null)
            {
                return Failure(
                    "Invalid optimization.");
            }

            if (!recommendation.CanApply)
            {
                return Failure(
                    "This optimization is review-only and cannot be applied automatically.");
            }

            try
            {
                return recommendation.Title switch
                {
                    "Performance Power Plan" =>
                        ApplyHighPerformancePowerPlan(),

                    "Enable Windows Game Mode" =>
                        ApplyGameMode(),

                    "Prevent sleep during workload" =>
                        ApplyPreventSleep(recommendation),

                    "Workload display timeout" =>
                        ApplyDisplayTimeout(recommendation),

                    _ =>
                        Failure(
                            "No apply handler exists for this optimization.")
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "BUILDCORE APPLY ERROR");

                Debug.WriteLine(
                    ex.ToString());

                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = "Optimization failed.",
                    Error = ex.Message
                };
            }
        }

        // ============================================================
        // HIGH PERFORMANCE POWER PLAN
        // ============================================================

        private static OptimizationApplyResult
            ApplyHighPerformancePowerPlan()
        {
            string output =
                RunProcess(
                    "powercfg",
                    "/setactive SCHEME_MIN");

            if (string.IsNullOrWhiteSpace(output))
            {
                // powercfg may succeed without producing useful output.
            }

            WindowsSystemData verification =
                WindowsSystemService.Scan();

            bool verified =
                verification.PowerPlan.Equals(
                    "High Performance",
                    StringComparison.OrdinalIgnoreCase);

            if (verified)
            {
                return new OptimizationApplyResult
                {
                    Success = true,
                    Verified = true,
                    Message =
                        "High Performance power plan enabled and verified."
                };
            }

            return new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message =
                    "Power plan command completed, but BuildCore could not verify the expected state."
            };
        }

        // ============================================================
        // WINDOWS GAME MODE
        // ============================================================

        private static OptimizationApplyResult
            ApplyGameMode()
        {
            using RegistryKey? key =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\GameBar");

            if (key == null)
            {
                return Failure(
                    "BuildCore could not access the Windows Game Bar registry settings.");
            }

            key.SetValue(
                "AutoGameModeEnabled",
                1,
                RegistryValueKind.DWord);

            key.Flush();

            WindowsSystemData verification =
                WindowsSystemService.Scan();

            if (verification.GameModeEnabled)
            {
                return new OptimizationApplyResult
                {
                    Success = true,
                    Verified = true,
                    Message =
                        "Windows Game Mode enabled and verified."
                };
            }

            return new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message =
                    "The setting was changed, but BuildCore could not verify Game Mode."
            };
        }

        // ============================================================
        // WORKLOAD POWER SETTINGS
        // ============================================================

        private static OptimizationApplyResult ApplyPreventSleep(
            OptimizationRecommendation recommendation)
        {
            return PowerOptimizationHandler.ApplyAcTimeout(
                "standby-timeout-ac",
                0);
        }

        private static OptimizationApplyResult ApplyDisplayTimeout(
            OptimizationRecommendation recommendation)
        {
            return PowerOptimizationHandler.ApplyAcTimeout(
                "monitor-timeout-ac",
                0);
        }

        // ============================================================
        // PROCESS EXECUTION
        // ============================================================

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

        private static OptimizationApplyResult
            Failure(string message)
        {
            return new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = message
            };
        }
    }
}