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

            foreach (Process process in Process.GetProcesses())
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
                        startTimeUtc = process.StartTime.ToUniversalTime();
                    }
                    catch
                    {
                    }

                    try
                    {
                        executablePath = process.MainModule?.FileName ?? "";
                    }
                    catch
                    {
                    }

                    processes.Add(new RunningProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = name,
                        StartTimeUtc = startTimeUtc,
                        ExecutablePath = executablePath
                    });
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            return processes
                .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.ProcessId)
                .ToList();
        }
    }
}
