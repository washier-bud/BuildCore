using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BuildCore
{
    public static class PowerOptimizationHandler
    {
        private const string QueryPattern =
            @"Current AC Power Setting Index:\s*0x([0-9A-Fa-f]+)";

        public static string GetAcTimeoutSeconds(string settingAlias)
        {
            try
            {
                string output = RunProcess(
                    "powercfg",
                    $"/query SCHEME_CURRENT {GetSubgroupAlias(settingAlias)} {GetSettingAlias(settingAlias)}");

                Match match = Regex.Match(
                    output,
                    QueryPattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    return "Unknown";

                if (!uint.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out uint seconds))
                {
                    return "Unknown";
                }

                return $"{seconds} seconds";
            }
            catch
            {
                return "Unknown";
            }
        }

        public static OptimizationApplyResult ApplyAcTimeout(
            string settingAlias,
            uint seconds)
        {
            try
            {
                RunProcess(
                    "powercfg",
                    $"/setacvalueindex SCHEME_CURRENT {GetSubgroupAlias(settingAlias)} {GetSettingAlias(settingAlias)} {seconds}");

                RunProcess(
                    "powercfg",
                    "/setactive SCHEME_CURRENT");

                string current =
                    GetAcTimeoutSeconds(settingAlias);

                bool verified =
                    current.Equals(
                        $"{seconds} seconds",
                        StringComparison.OrdinalIgnoreCase);

                return new OptimizationApplyResult
                {
                    Success = verified,
                    Verified = verified,
                    Message = verified
                        ? $"AC {GetFriendlyName(settingAlias)} set to {seconds} seconds and verified."
                        : $"AC {GetFriendlyName(settingAlias)} was changed, but BuildCore could not verify the requested value.",
                    Error = verified ? null : "Power policy verification failed."
                };
            }
            catch (Exception ex)
            {
                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = $"Unable to change AC {GetFriendlyName(settingAlias)}.",
                    Error = ex.Message
                };
            }
        }

        public static OptimizationRestoreResult RestoreAcTimeout(
            string settingAlias,
            string beforeValue)
        {
            if (!TryParseSeconds(beforeValue, out uint seconds))
            {
                return new OptimizationRestoreResult
                {
                    Success = false,
                    Verified = false,
                    Message = "The original power timeout could not be parsed.",
                    Error = "Invalid saved timeout value."
                };
            }

            try
            {
                RunProcess(
                    "powercfg",
                    $"/setacvalueindex SCHEME_CURRENT {GetSubgroupAlias(settingAlias)} {GetSettingAlias(settingAlias)} {seconds}");

                RunProcess(
                    "powercfg",
                    "/setactive SCHEME_CURRENT");

                string current =
                    GetAcTimeoutSeconds(settingAlias);

                bool verified =
                    current.Equals(
                        $"{seconds} seconds",
                        StringComparison.OrdinalIgnoreCase);

                return new OptimizationRestoreResult
                {
                    Success = verified,
                    Verified = verified,
                    Message = verified
                        ? $"AC {GetFriendlyName(settingAlias)} restored to {seconds} seconds and verified."
                        : $"BuildCore attempted to restore AC {GetFriendlyName(settingAlias)}, but verification failed.",
                    Error = verified ? null : "Power policy restore verification failed."
                };
            }
            catch (Exception ex)
            {
                return new OptimizationRestoreResult
                {
                    Success = false,
                    Verified = false,
                    Message = $"Unable to restore AC {GetFriendlyName(settingAlias)}.",
                    Error = ex.Message
                };
            }
        }

        private static string GetSubgroupAlias(string settingAlias) =>
            settingAlias switch
            {
                "standby-timeout-ac" => "SUB_SLEEP",
                "monitor-timeout-ac" => "SUB_VIDEO",
                _ => throw new ArgumentException(
                    $"Unsupported power setting '{settingAlias}'.",
                    nameof(settingAlias))
            };

        private static string GetSettingAlias(string settingAlias) =>
            settingAlias switch
            {
                "standby-timeout-ac" => "STANDBYIDLE",
                "monitor-timeout-ac" => "VIDEOIDLE",
                _ => throw new ArgumentException(
                    $"Unsupported power setting '{settingAlias}'.",
                    nameof(settingAlias))
            };

        private static string GetFriendlyName(string settingAlias) =>
            settingAlias switch
            {
                "standby-timeout-ac" => "sleep timeout",
                "monitor-timeout-ac" => "display timeout",
                _ => "power setting"
            };

        private static bool TryParseSeconds(
            string value,
            out uint seconds)
        {
            seconds = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string number = value
                .Replace("seconds", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            return uint.TryParse(
                number,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out seconds);
        }

        private static string RunProcess(
            string fileName,
            string arguments)
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"powercfg exited with code {process.ExitCode}."
                        : error.Trim());
            }

            return output;
        }
    }
}