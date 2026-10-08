using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        }

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

        private const string PowerManagementScript =
            "$items = @(Get-NetAdapter -Physical -ErrorAction Stop | " +
            "ForEach-Object { " +
            "$pm = Get-NetAdapterPowerManagement -Name $_.Name -ErrorAction SilentlyContinue; " +
            "if ($null -ne $pm) { " +
            "[PSCustomObject]@{ Name=$_.Name; InterfaceDescription=$_.InterfaceDescription; AllowComputerToTurnOffDevice=[string]$pm.AllowComputerToTurnOffDevice } " +
            "} " +
            "} | Where-Object { $_.AllowComputerToTurnOffDevice -eq 'Enabled' -or $_.AllowComputerToTurnOffDevice -eq 'Disabled' }); " +
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
                        CurrentValue = "No supported physical network adapters found",
                        RecommendedValue = "Disabled",
                        Description = "Disable the Windows network-adapter power-management setting on supported physical adapters.",
                        Reason = "BuildCore could not find a physical adapter exposing the power-management control.",
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

                int enabled = states.Count(state =>
                    state.AllowComputerToTurnOffDevice.Equals(
                        "Enabled",
                        StringComparison.OrdinalIgnoreCase));

                string current =
                    enabled == 0
                        ? $"Disabled on {states.Count} supported adapter(s)"
                        : $"Enabled on {enabled} of {states.Count} supported adapter(s)";

                return new OptimizationRecommendation
                {
                    Title = "Disable adapter power saving",
                    Category = OptimizationCategory.Network,
                    CurrentValue = current,
                    RecommendedValue = $"Disabled on {states.Count} supported adapter(s)",
                    Description = "Disable the Windows network-adapter power-management setting on supported physical adapters.",
                    Reason = "BuildCore changes the adapter power-management state only on physical adapters that expose the setting, then verifies the result and records the original state for rollback.",
                    Risk = OptimizationRisk.Medium,
                    Impact = OptimizationImpact.Low,
                    CanAnalyze = true,
                    CanApply = enabled > 0,
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
            {
                throw new InvalidOperationException(
                    "No supported physical network adapter power-management settings were found.");
            }

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
                    "if ($null -ne $pm -and ($pm.AllowComputerToTurnOffDevice -eq 'Enabled' -or $pm.AllowComputerToTurnOffDevice -eq 'Disabled')) { " +
                    "if ($pm.AllowComputerToTurnOffDevice -eq 'Enabled') { " +
                    "$pm.AllowComputerToTurnOffDevice='Disabled'; " +
                    "$pm | Set-NetAdapterPowerManagement -NoRestart -ErrorAction Stop; " +
                    "$changed++ " +
                    "} " +
                    "} " +
                    "}; " +
                    "$changed");

                List<AdapterState> states = CaptureStates();
                int remaining = states.Count(state =>
                    state.AllowComputerToTurnOffDevice.Equals(
                        "Enabled",
                        StringComparison.OrdinalIgnoreCase));

                if (remaining == 0 && states.Count > 0)
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
                    Message = $"BuildCore changed the adapter power setting, but {remaining} supported adapter(s) still report it as enabled.",
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
                    JsonSerializer.Deserialize<List<AdapterState>>(
                        beforeValue,
                        JsonOptions);

                if (states == null || states.Count == 0)
                {
                    return RestoreFailure(
                        "The original network-adapter power state was not recorded.");
                }

                string stateJson =
                    JsonSerializer.Serialize(states, JsonOptions);

                string encoded =
                    Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes(stateJson));

                string script =
                    "$ErrorActionPreference='Stop'; " +
                    "$states=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" +
                    encoded +
                    "')); " +
                    "$states=$states | ConvertFrom-Json; " +
                    "foreach($state in @($states)) { " +
                    "$pm=Get-NetAdapterPowerManagement -Name $state.Name -ErrorAction SilentlyContinue; " +
                    "if($null -eq $pm) { throw ('Network adapter not found: ' + $state.Name) }; " +
                    "if($state.AllowComputerToTurnOffDevice -eq 'Enabled' -or $state.AllowComputerToTurnOffDevice -eq 'Disabled') { " +
                    "$pm.AllowComputerToTurnOffDevice=[string]$state.AllowComputerToTurnOffDevice; " +
                    "$pm | Set-NetAdapterPowerManagement -NoRestart -ErrorAction Stop; " +
                    "} " +
                    "}";

                RunPowerShell(script);

                List<AdapterState> verified =
                    CaptureStates();

                foreach (AdapterState original in states)
                {
                    AdapterState? current =
                        verified.FirstOrDefault(
                            state => state.Name.Equals(
                                original.Name,
                                StringComparison.OrdinalIgnoreCase));

                    if (current == null ||
                        !current.AllowComputerToTurnOffDevice.Equals(
                            original.AllowComputerToTurnOffDevice,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return RestoreFailure(
                            $"BuildCore restored the adapter power setting, but verification failed for '{original.Name}'.");
                    }
                }

                return new OptimizationRestoreResult
                {
                    Success = true,
                    Verified = true,
                    Message = $"Original adapter power-saving state restored and verified on {states.Count} adapter(s)."
                };
            }
            catch (Exception ex)
            {
                return ApplyFailure(
                    "BuildCore could not restore the original adapter power-saving state.",
                    ex.Message);
            }
        }

        private static List<AdapterState> CaptureStates()
        {
            string json = RunPowerShell(PowerManagementScript);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<AdapterState>();
            }

            try
            {
                List<AdapterState>? states =
                    JsonSerializer.Deserialize<List<AdapterState>>(
                        json,
                        JsonOptions);

                return states ?? new List<AdapterState>();
            }
            catch
            {
                AdapterState? single =
                    JsonSerializer.Deserialize<AdapterState>(
                        json,
                        JsonOptions);

                return single == null
                    ? new List<AdapterState>()
                    : new List<AdapterState> { single };
            }
        }

        private static string RunPowerShell(string script)
        {
            string encodedCommand =
                Convert.ToBase64String(
                    System.Text.Encoding.Unicode.GetBytes(script));

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

            string output =
                process.StandardOutput.ReadToEnd();

            string error =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"PowerShell exited with code {process.ExitCode}."
                        : error.Trim());
            }

            return output.Trim();
        }

        private static OptimizationApplyResult ApplyFailure(
            string message,
            string? error = null)
        {
            return new OptimizationApplyResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = error ?? message
            };
        }

        private static OptimizationRestoreResult RestoreFailure(
            string message,
            string? error = null)
        {
            return new OptimizationRestoreResult
            {
                Success = false,
                Verified = false,
                Message = message,
                Error = error ?? message
            };
        }
    }
}
