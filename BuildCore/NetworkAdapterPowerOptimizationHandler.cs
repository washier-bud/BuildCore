using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace BuildCore
{
    public static class NetworkAdapterPowerOptimizationHandler
    {
        private sealed class AdapterState
        {
            public string Name { get; set; } = "";
            public string InterfaceDescription { get; set; } = "";
            public string AllowComputerToTurnOffDevice { get; set; } = "";
            public string ArpOffload { get; set; } = "";
            public string D0PacketCoalescing { get; set; } = "";
            public string DeviceSleepOnDisconnect { get; set; } = "";
            public string NSOffload { get; set; } = "";
            public string RsnRekeyOffload { get; set; } = "";
            public string SelectiveSuspend { get; set; } = "";
            public string WakeOnMagicPacket { get; set; } = "";
            public string WakeOnPattern { get; set; } = "";
        }

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

        private const string CaptureScript =
            "$items = @(Get-NetAdapter -Physical -ErrorAction Stop | " +
            "ForEach-Object { " +
            "$adapter = $_; " +
            "$pm = Get-NetAdapterPowerManagement -Name $adapter.Name -ErrorAction SilentlyContinue; " +
            "if ($null -ne $pm) { " +
            "[PSCustomObject]@{ " +
            "Name=$adapter.Name; InterfaceDescription=$adapter.InterfaceDescription; " +
            "AllowComputerToTurnOffDevice=[string]$pm.AllowComputerToTurnOffDevice; " +
            "ArpOffload=[string]$pm.ArpOffload; D0PacketCoalescing=[string]$pm.D0PacketCoalescing; " +
            "DeviceSleepOnDisconnect=[string]$pm.DeviceSleepOnDisconnect; NSOffload=[string]$pm.NSOffload; " +
            "RsnRekeyOffload=[string]$pm.RsnRekeyOffload; SelectiveSuspend=[string]$pm.SelectiveSuspend; " +
            "WakeOnMagicPacket=[string]$pm.WakeOnMagicPacket; WakeOnPattern=[string]$pm.WakeOnPattern " +
            "} } } | " +
            "Where-Object { $_.AllowComputerToTurnOffDevice -ne 'Unsupported' -or " +
            "$_.ArpOffload -ne 'Unsupported' -or $_.D0PacketCoalescing -ne 'Unsupported' -or " +
            "$_.DeviceSleepOnDisconnect -ne 'Unsupported' -or $_.NSOffload -ne 'Unsupported' -or " +
            "$_.RsnRekeyOffload -ne 'Unsupported' -or $_.SelectiveSuspend -ne 'Unsupported' -or " +
            "$_.WakeOnMagicPacket -ne 'Unsupported' -or $_.WakeOnPattern -ne 'Unsupported' }; " +
            "@($items) | ConvertTo-Json -Compress";

        public static OptimizationRecommendation? CreateRecommendation()
        {
            try
            {
                List<AdapterState> states = CaptureStates();

                if (states.Count == 0)
                {
                    return new OptimizationRecommendation
                    {
                        Title = "Disable adapter power saving",
                        Category = OptimizationCategory.Network,
                        CurrentValue = "No physical network adapter with supported power-management controls found",
                        RecommendedValue = "Disabled",
                        Description = "Disable supported Windows network-adapter power-management features on physical adapters.",
                        Reason = "BuildCore could not find a physical adapter exposing usable power-management controls.",
                        Risk = OptimizationRisk.Medium,
                        Impact = OptimizationImpact.Low,
                        CanAnalyze = true,
                        CanApply = false,
                        CanTest = false,
                        RequiresReboot = false,
                        RollbackSupported = true,
                        TestType = OptimizationTestType.None,
                        TestDescription = "No supported adapter was detected."
                    };
                }

                int enabled = states.Count(HasEnabledPowerManagement);

                return new OptimizationRecommendation
                {
                    Title = "Disable adapter power saving",
                    Category = OptimizationCategory.Network,
                    CurrentValue = enabled == 0
                        ? $"Power saving disabled on {states.Count} supported adapter(s)"
                        : $"Power saving enabled on {enabled} of {states.Count} supported adapter(s)",
                    RecommendedValue = $"Power saving disabled on {states.Count} supported adapter(s)",
                    Description = "Disable supported Windows network-adapter power-management features on physical adapters.",
                    Reason = "BuildCore captures supported adapter power-management state, disables supported features without restarting the adapter, verifies the result, and stores the original state for rollback.",
                    Risk = OptimizationRisk.Medium,
                    Impact = OptimizationImpact.Low,
                    CanAnalyze = true,
                    CanApply = true,
                    CanTest = false,
                    RequiresReboot = false,
                    RollbackSupported = true,
                    TestType = OptimizationTestType.None,
                    TestDescription = "The adapter power-management state is verified after the change."
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BUILDCORE NETWORK POWER ANALYSIS ERROR: {ex}");
                return null;
            }
        }

        public static string CaptureTransactionState()
        {
            List<AdapterState> states = CaptureStates();
            if (states.Count == 0)
                throw new InvalidOperationException("No supported physical network adapter power-management settings were found.");

            return JsonSerializer.Serialize(states, JsonOptions);
        }

        public static OptimizationApplyResult Apply()
        {
            try
            {
                string output = RunPowerShell(
                    "$ErrorActionPreference='Stop'; " +
                    "$changed=0; " +
                    "Get-NetAdapter -Physical -ErrorAction Stop | ForEach-Object { " +
                    "$pm=Get-NetAdapterPowerManagement -Name $_.Name -ErrorAction SilentlyContinue; " +
                    "if ($null -ne $pm) { " +
                    "Disable-NetAdapterPowerManagement -Name $_.Name -NoRestart -ErrorAction Stop; $changed++ } }; " +
                    "$changed");

                List<AdapterState> states = CaptureStates();
                int remaining = states.Count(HasEnabledPowerManagement);

                if (states.Count > 0 && remaining == 0)
                {
                    return new OptimizationApplyResult
                    {
                        Success = true,
                        Verified = true,
                        Message = $"Adapter power saving disabled and verified on {states.Count} supported physical adapter(s)."
                    };
                }

                return new OptimizationApplyResult
                {
                    Success = false,
                    Verified = false,
                    Message = $"BuildCore changed adapter power-management settings, but {remaining} adapter(s) still expose enabled power-management features.",
                    Error = string.IsNullOrWhiteSpace(output) ? null : output.Trim()
                };
            }
            catch (Exception ex)
            {
                return ApplyFailure(
                    "BuildCore could not disable adapter power saving. Run BuildCore as Administrator and verify that the Windows NetAdapter power-management cmdlets are available.",
                    ex.Message);
            }
        }

        public static OptimizationRestoreResult Restore(string beforeValue)
        {
            try
            {
                List<AdapterState>? states =
                    JsonSerializer.Deserialize<List<AdapterState>>(beforeValue, JsonOptions);

                if (states == null || states.Count == 0)
                    return RestoreFailure("The original network-adapter power state was not recorded.");

                string stateJson = JsonSerializer.Serialize(states, JsonOptions);
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

                string script =
                    "$ErrorActionPreference='Stop'; " +
                    "$states=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "')) | ConvertFrom-Json; " +
                    "foreach($state in @($states)) { " +
                    "$name=$state.Name; " +
                    "if($null -eq (Get-NetAdapter -Name $name -ErrorAction SilentlyContinue)) { throw ('Network adapter not found: ' + $name) }; " +
                    "Disable-NetAdapterPowerManagement -Name $name -NoRestart -ErrorAction Stop; " +
                    "if($state.ArpOffload -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -ArpOffload -NoRestart -ErrorAction Stop}elseif($state.ArpOffload -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -ArpOffload -NoRestart -ErrorAction Stop}; " +
                    "if($state.D0PacketCoalescing -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -D0PacketCoalescing -NoRestart -ErrorAction Stop}elseif($state.D0PacketCoalescing -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -D0PacketCoalescing -NoRestart -ErrorAction Stop}; " +
                    "if($state.DeviceSleepOnDisconnect -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -DeviceSleepOnDisconnect -NoRestart -ErrorAction Stop}elseif($state.DeviceSleepOnDisconnect -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -DeviceSleepOnDisconnect -NoRestart -ErrorAction Stop}; " +
                    "if($state.NSOffload -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -NSOffload -NoRestart -ErrorAction Stop}elseif($state.NSOffload -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -NSOffload -NoRestart -ErrorAction Stop}; " +
                    "if($state.RsnRekeyOffload -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -RsnRekeyOffload -NoRestart -ErrorAction Stop}elseif($state.RsnRekeyOffload -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -RsnRekeyOffload -NoRestart -ErrorAction Stop}; " +
                    "if($state.SelectiveSuspend -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -SelectiveSuspend -NoRestart -ErrorAction Stop}elseif($state.SelectiveSuspend -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -SelectiveSuspend -NoRestart -ErrorAction Stop}; " +
                    "if($state.WakeOnMagicPacket -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -WakeOnMagicPacket -NoRestart -ErrorAction Stop}elseif($state.WakeOnMagicPacket -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -WakeOnMagicPacket -NoRestart -ErrorAction Stop}; " +
                    "if($state.WakeOnPattern -eq 'Enabled'){Enable-NetAdapterPowerManagement -Name $name -WakeOnPattern -NoRestart -ErrorAction Stop}elseif($state.WakeOnPattern -eq 'Disabled'){Disable-NetAdapterPowerManagement -Name $name -WakeOnPattern -NoRestart -ErrorAction Stop}; " +
                    "}";

                RunPowerShell(script);

                List<AdapterState> verified = CaptureStates();
                foreach (AdapterState original in states)
                {
                    AdapterState? current = verified.FirstOrDefault(
                        state => state.Name.Equals(original.Name, StringComparison.OrdinalIgnoreCase));

                    if (current == null || !StatesMatch(original, current))
                        return RestoreFailure($"BuildCore restored adapter power-management settings, but verification failed for '{original.Name}'.");
                }

                return new OptimizationRestoreResult
                {
                    Success = true,
                    Verified = true,
                    Message = $"Original adapter power-management state restored and verified on {states.Count} adapter(s)."
                };
            }
            catch (Exception ex)
            {
                return RestoreFailure(
                    "BuildCore could not restore the original adapter power-management state.",
                    ex.Message);
            }
        }

        private static List<AdapterState> CaptureStates()
        {
            string json = RunPowerShell(CaptureScript);
            if (string.IsNullOrWhiteSpace(json))
                return new List<AdapterState>();

            try
            {
                List<AdapterState>? states =
                    JsonSerializer.Deserialize<List<AdapterState>>(json, JsonOptions);
                return states ?? new List<AdapterState>();
            }
            catch
            {
                AdapterState? single =
                    JsonSerializer.Deserialize<AdapterState>(json, JsonOptions);
                return single == null ? new List<AdapterState>() : new List<AdapterState> { single };
            }
        }

        private static bool HasEnabledPowerManagement(AdapterState state)
        {
            return IsEnabled(state.ArpOffload) ||
                   IsEnabled(state.D0PacketCoalescing) ||
                   IsEnabled(state.DeviceSleepOnDisconnect) ||
                   IsEnabled(state.NSOffload) ||
                   IsEnabled(state.RsnRekeyOffload) ||
                   IsEnabled(state.SelectiveSuspend) ||
                   IsEnabled(state.WakeOnMagicPacket) ||
                   IsEnabled(state.WakeOnPattern);
        }

        private static bool StatesMatch(AdapterState expected, AdapterState actual)
        {
            return SameState(expected.ArpOffload, actual.ArpOffload) &&
                   SameState(expected.D0PacketCoalescing, actual.D0PacketCoalescing) &&
                   SameState(expected.DeviceSleepOnDisconnect, actual.DeviceSleepOnDisconnect) &&
                   SameState(expected.NSOffload, actual.NSOffload) &&
                   SameState(expected.RsnRekeyOffload, actual.RsnRekeyOffload) &&
                   SameState(expected.SelectiveSuspend, actual.SelectiveSuspend) &&
                   SameState(expected.WakeOnMagicPacket, actual.WakeOnMagicPacket) &&
                   SameState(expected.WakeOnPattern, actual.WakeOnPattern);
        }

        private static bool SameState(string expected, string actual)
        {
            if (expected.Equals("Unsupported", StringComparison.OrdinalIgnoreCase))
                return true;

            if (expected.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                return actual.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ||
                       actual.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

            return expected.Equals(actual, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEnabled(string value) =>
            value.Equals("Enabled", StringComparison.OrdinalIgnoreCase);

        private static string RunPowerShell(string script)
        {
            string encodedCommand =
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
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
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"PowerShell exited with code {process.ExitCode}."
                        : error.Trim());

            return output.Trim();
        }

        private static OptimizationApplyResult ApplyFailure(string message, string? error = null) =>
            new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = error ?? message
            };

        private static OptimizationRestoreResult RestoreFailure(string message, string? error = null) =>
            new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = error ?? message
            };
    }
}
