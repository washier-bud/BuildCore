using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BuildCore
{
    public static class RunningProcessService
    {
        public static IReadOnlyList<RunningProcessInfo> GetRunningProcesses()
        {
            var processes = new List<RunningProcessInfo>();

            Process[] systemProcesses;

            try
            {
                systemProcesses = Process.GetProcesses();
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE PROCESS ENUMERATION ACCESS ERROR: {ex}");

                return processes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"BUILDCORE PROCESS ENUMERATION ERROR: {ex}");

                return processes;
            }

            foreach (Process process in systemProcesses)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    string name = process.ProcessName;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    DateTime? startTimeUtc = null;
                    string executablePath = "";

                    try
                    {
                        startTimeUtc =
                            process.StartTime.ToUniversalTime();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"BUILDCORE PROCESS START TIME ERROR " +
                            $"({process.Id}): {ex.Message}");
                    }

                    try
                    {
                        executablePath =
                            process.MainModule?.FileName ?? "";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"BUILDCORE PROCESS PATH ACCESS ERROR " +
                            $"({process.Id}): {ex.Message}");
                    }

                    processes.Add(new RunningProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = name,
                        StartTimeUtc = startTimeUtc,
                        ExecutablePath = executablePath
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"BUILDCORE PROCESS READ ERROR: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            return processes
                .OrderBy(
                    p => p.ProcessName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.ProcessId)
                .ToList();
        }
    }
}