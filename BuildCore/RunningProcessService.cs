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

                    processes.Add(new RunningProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = name
                    });
                }
                catch
                {
                    // Some protected/system processes cannot be queried.
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
