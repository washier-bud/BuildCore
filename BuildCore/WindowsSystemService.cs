using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BuildCore
{
    public class WindowsSystemData
    {
        public string WindowsVersion { get; set; } = "Unknown";
        public string WindowsBuild { get; set; } = "Unknown";

        public string PowerPlan { get; set; } = "Unknown";

        public bool GameModeEnabled { get; set; }
        public bool HagsEnabled { get; set; }
        public bool MemoryIntegrityEnabled { get; set; }

        public bool DefenderEnabled { get; set; }

        public bool XboxGameBarEnabled { get; set; }

        public string WindowsUpdateStatus { get; set; } = "Unknown";

        public string ProcessorCount { get; set; } = "Unknown";
        public string LogicalProcessorCount { get; set; } = "Unknown";
    }

    public sealed class ActivePowerPlanState
    {
        public string Guid { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public static class WindowsSystemService
    {
        public static string CaptureActivePowerPlanState()
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Unable to query the active power plan." : error.Trim());

            var guidMatch = System.Text.RegularExpressions.Regex.Match(
                output,
                @"\b([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\b");

            if (!guidMatch.Success)
                throw new InvalidOperationException("The active power-plan GUID could not be parsed.");

            string name = "";
            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                output,
                @"\(([^\r\n]*)\)");

            if (nameMatch.Success)
                name = nameMatch.Groups[1].Value.Trim();

            return JsonSerializer.Serialize(new ActivePowerPlanState
            {
                Guid = guidMatch.Groups[1].Value,
                Name = name
            });
        }

        public static string CaptureGameModeState()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                if (key == null || key.GetValue("AutoGameModeEnabled") == null)
                    return "missing";

                return Convert.ToInt32(key.GetValue("AutoGameModeEnabled")) == 1
                    ? "Enabled"
                    : "Disabled";
            }
            catch
            {
                return "Unknown";
            }
        }

        public static WindowsSystemData Scan()
        {
            var data = new WindowsSystemData();

            ReadWindowsVersion(data);
            ReadPowerPlan(data);
            ReadGameMode(data);
            ReadHags(data);
            ReadMemoryIntegrity(data);
            ReadDefender(data);
            ReadXboxGameBar(data);
            ReadWindowsUpdate(data);
            ReadProcessorInformation(data);

            return data;
        }

        private static void ReadWindowsVersion(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                if (key == null)
                    return;

                string productName =
                    key.GetValue("ProductName")?.ToString()
                    ?? "Windows";

                string displayVersion =
                    key.GetValue("DisplayVersion")?.ToString()
                    ?? "";

                string build =
                    key.GetValue("CurrentBuild")?.ToString()
                    ?? "";

                string ubr =
                    key.GetValue("UBR")?.ToString()
                    ?? "";

                data.WindowsVersion =
                    $"{productName} {displayVersion}".Trim();

                data.WindowsBuild =
                    $"{build}.{ubr}".Trim('.');
            }
            catch
            {
                data.WindowsVersion = "Unknown";
                data.WindowsBuild = "Unknown";
            }
        }

        private static void ReadPowerPlan(
            WindowsSystemData data)
        {
            try
            {
                using var process =
                    new Process();

                process.StartInfo.FileName =
                    "powercfg";

                process.StartInfo.Arguments =
                    "/getactivescheme";

                process.StartInfo.UseShellExecute =
                    false;

                process.StartInfo.CreateNoWindow =
                    true;

                process.StartInfo.RedirectStandardOutput =
                    true;

                process.Start();

                string output =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                if (output.Contains(
                    "Ultimate Performance",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.PowerPlan =
                        "Ultimate Performance";
                }
                else if (output.Contains(
                    "High performance",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.PowerPlan =
                        "High Performance";
                }
                else if (output.Contains(
                    "Balanced",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.PowerPlan =
                        "Balanced";
                }
                else if (!string.IsNullOrWhiteSpace(output))
                {
                    data.PowerPlan =
                        output.Trim();
                }
            }
            catch
            {
                data.PowerPlan = "Unknown";
            }
        }

        private static void ReadGameMode(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\GameBar");

                if (key == null)
                {
                    data.GameModeEnabled = false;
                    return;
                }

                object? value =
                    key.GetValue("AutoGameModeEnabled");

                if (value != null)
                {
                    data.GameModeEnabled =
                        Convert.ToInt32(value) == 1;
                }
            }
            catch
            {
                data.GameModeEnabled = false;
            }
        }

        private static void ReadHags(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\DirectX\UserGpuPreferences");

                if (key == null)
                {
                    data.HagsEnabled = false;
                    return;
                }

                object? value =
                    key.GetValue("DirectXUserGlobalSettings");

                if (value == null)
                    return;

                string settings =
                    value.ToString() ?? "";

                data.HagsEnabled =
                    settings.Contains(
                        "HwSchMode=2",
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                data.HagsEnabled = false;
            }
        }

        private static void ReadMemoryIntegrity(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.LocalMachine.OpenSubKey(
                        @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");

                if (key == null)
                {
                    data.MemoryIntegrityEnabled = false;
                    return;
                }

                object? value =
                    key.GetValue("Enabled");

                if (value != null)
                {
                    data.MemoryIntegrityEnabled =
                        Convert.ToInt32(value) == 1;
                }
            }
            catch
            {
                data.MemoryIntegrityEnabled = false;
            }
        }

        private static void ReadDefender(
            WindowsSystemData data)
        {
            try
            {
                using var searcher =
                    new ManagementObjectSearcher(
                        @"root\Microsoft\Windows\Defender",
                        "SELECT AntivirusEnabled FROM MSFT_MpComputerStatus");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["AntivirusEnabled"] != null)
                    {
                        data.DefenderEnabled =
                            Convert.ToBoolean(
                                obj["AntivirusEnabled"]);
                    }

                    break;
                }
            }
            catch
            {
                data.DefenderEnabled = false;
            }
        }

        private static void ReadXboxGameBar(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\GameDVR");

                if (key == null)
                {
                    data.XboxGameBarEnabled = false;
                    return;
                }

                object? value =
                    key.GetValue("AppCaptureEnabled");

                if (value != null)
                {
                    data.XboxGameBarEnabled =
                        Convert.ToInt32(value) == 1;
                }
            }
            catch
            {
                data.XboxGameBarEnabled = false;
            }
        }

        private static void ReadWindowsUpdate(
            WindowsSystemData data)
        {
            try
            {
                using var key =
                    Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy");

                data.WindowsUpdateStatus =
                    key != null
                        ? "Configured"
                        : "Default";
            }
            catch
            {
                data.WindowsUpdateStatus =
                    "Unknown";
            }
        }

        private static void ReadProcessorInformation(
            WindowsSystemData data)
        {
            try
            {
                using var searcher =
                    new ManagementObjectSearcher(
                        "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    data.ProcessorCount =
                        obj["NumberOfCores"]?.ToString()
                        ?? "Unknown";

                    data.LogicalProcessorCount =
                        obj["NumberOfLogicalProcessors"]?.ToString()
                        ?? "Unknown";

                    break;
                }
            }
            catch
            {
                data.ProcessorCount = "Unknown";
                data.LogicalProcessorCount = "Unknown";
            }
        }
    }
}