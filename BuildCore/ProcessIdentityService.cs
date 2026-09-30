using System;
using System.Diagnostics;
using System.IO;

namespace BuildCore
{
    public static class ProcessIdentityService
    {
        public static RunningProcessInfo? Capture(int processId)
        {
            if (processId <= 0)
                return null;

            try
            {
                using Process process = Process.GetProcessById(processId);

                if (process.HasExited)
                    return null;

                DateTime startTimeUtc = process.StartTime.ToUniversalTime();
                string executablePath = "";

                try
                {
                    executablePath = process.MainModule?.FileName ?? "";
                }
                catch
                {
                }

                if (string.IsNullOrWhiteSpace(executablePath))
                    return null;

                return new RunningProcessInfo
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    StartTimeUtc = startTimeUtc,
                    ExecutablePath = NormalizePath(executablePath)
                };
            }
            catch
            {
                return null;
            }
        }

        public static bool Matches(
            int processId,
            DateTime? expectedStartTimeUtc,
            string expectedExecutablePath)
        {
            if (processId <= 0 ||
                !expectedStartTimeUtc.HasValue ||
                string.IsNullOrWhiteSpace(expectedExecutablePath))
            {
                return false;
            }

            RunningProcessInfo? current = Capture(processId);

            if (current == null || !current.HasIdentity)
                return false;

            if (current.StartTimeUtc!.Value != expectedStartTimeUtc.Value)
                return false;

            return string.Equals(
                NormalizePath(current.ExecutablePath),
                NormalizePath(expectedExecutablePath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
