using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BuildCore
{
    public static class WorkloadBenchmarkStorageService
    {
        private static readonly string BenchmarkDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BuildCore",
                "WorkloadBenchmarks");

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        public static void SaveResult(WorkloadBenchmarkResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (string.IsNullOrWhiteSpace(result.ResultId))
                throw new ArgumentException(
                    "Workload benchmark has no valid ID.",
                    nameof(result));

            Directory.CreateDirectory(BenchmarkDirectory);

            File.WriteAllText(
                GetResultPath(result.ResultId),
                JsonSerializer.Serialize(result, JsonOptions));
        }

        public static WorkloadBenchmarkResult? LoadResult(string resultId)
        {
            try
            {
                string path = GetResultPath(resultId);

                if (!File.Exists(path))
                    return null;

                return JsonSerializer.Deserialize<WorkloadBenchmarkResult>(
                    File.ReadAllText(path),
                    JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static List<WorkloadBenchmarkResult> GetResults()
        {
            var results = new List<WorkloadBenchmarkResult>();

            if (!Directory.Exists(BenchmarkDirectory))
                return results;

            foreach (string file in Directory.GetFiles(
                BenchmarkDirectory,
                "*.json"))
            {
                try
                {
                    WorkloadBenchmarkResult? result =
                        JsonSerializer.Deserialize<WorkloadBenchmarkResult>(
                            File.ReadAllText(file),
                            JsonOptions);

                    if (result != null &&
                        !string.IsNullOrWhiteSpace(result.ResultId))
                    {
                        results.Add(result);
                    }
                }
                catch
                {
                    // Ignore corrupted history entries.
                }
            }

            return results
                .OrderByDescending(result => result.StartedAt)
                .ToList();
        }

        public static bool DeleteResult(string resultId)
        {
            try
            {
                string path = GetResultPath(resultId);

                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateResult(string resultId)
        {
            WorkloadBenchmarkResult? result = LoadResult(resultId);

            return result != null &&
                   !string.IsNullOrWhiteSpace(result.ResultId) &&
                   !string.IsNullOrWhiteSpace(result.WorkloadId) &&
                   !string.IsNullOrWhiteSpace(result.WorkloadName) &&
                   result.StartedAt != default &&
                   result.CompletedAt >= result.StartedAt &&
                   result.RequestedRuns > 0;
        }

        public static string GetBenchmarkDirectory()
        {
            Directory.CreateDirectory(BenchmarkDirectory);
            return BenchmarkDirectory;
        }

        private static string GetResultPath(string resultId)
        {
            if (string.IsNullOrWhiteSpace(resultId))
                throw new ArgumentException(
                    "Invalid workload benchmark ID.",
                    nameof(resultId));

            foreach (char character in resultId)
            {
                if (!char.IsLetterOrDigit(character))
                    throw new ArgumentException(
                        "Invalid workload benchmark ID.",
                        nameof(resultId));
            }

            return Path.Combine(
                BenchmarkDirectory,
                resultId + ".json");
        }
    }
}
