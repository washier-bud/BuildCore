using System;
using System.Security.Cryptography;
using System.Text;

namespace BuildCore
{
    public static class WorkloadFingerprintService
    {
        public static string Calculate(BenchmarkWorkload workload)
        {
            if (workload == null)
                throw new ArgumentNullException(nameof(workload));

            string canonical = string.Join("|",
                workload.Name,
                workload.Type,
                workload.Description,
                workload.DurationSeconds,
                workload.RunCount,
                workload.SampleIntervalMilliseconds,
                workload.DelayBetweenRunsMilliseconds,
                workload.RequiresInteractiveWorkload,
                NormalizePath(workload.TargetProcessPath));

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            try
            {
                return System.IO.Path.GetFullPath(path).TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}