using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BuildCore
{
    public static class OptimizationTestStorageService
    {
        private static readonly string TestDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BuildCore",
                "OptimizationTests");

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        // ============================================================
        // SAVE
        // ============================================================

        public static void SaveTest(
            OptimizationBenchmarkResult test)
        {
            if (test == null)
                throw new ArgumentNullException(nameof(test));

            if (string.IsNullOrWhiteSpace(test.TestId))
                throw new ArgumentException(
                    "Optimization test has no valid ID.",
                    nameof(test));

            Directory.CreateDirectory(
                TestDirectory);

            string filePath =
                GetTestPath(test.TestId);

            string json =
                JsonSerializer.Serialize(
                    test,
                    JsonOptions);

            File.WriteAllText(
                filePath,
                json);
        }

        // ============================================================
        // LOAD
        // ============================================================

        public static OptimizationBenchmarkResult?
            LoadTest(
                string testId)
        {
            try
            {
                string filePath =
                    GetTestPath(testId);

                if (!File.Exists(filePath))
                    return null;

                string json =
                    File.ReadAllText(filePath);

                return JsonSerializer.Deserialize<
                    OptimizationBenchmarkResult>(
                        json,
                        JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        // ============================================================
        // LIST
        // ============================================================

        public static List<OptimizationBenchmarkResult>
            GetTests()
        {
            var tests =
                new List<OptimizationBenchmarkResult>();

            if (!Directory.Exists(
                TestDirectory))
            {
                return tests;
            }

            foreach (
                string file
                in Directory.GetFiles(
                    TestDirectory,
                    "*.json"))
            {
                try
                {
                    string json =
                        File.ReadAllText(file);

                    OptimizationBenchmarkResult? test =
                        JsonSerializer.Deserialize<
                            OptimizationBenchmarkResult>(
                            json,
                            JsonOptions);

                    if (test == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(
                        test.TestId))
                    {
                        continue;
                    }

                    test = NormalizeLoadedTest(test);\n\n                    if (test == null)\n                        continue;\n\n                    tests.Add(test);
                }
                catch
                {
                    // Ignore corrupted test files.
                }
            }

            return tests
                .OrderByDescending(
                    test => test.StartedAt)
                .ToList();
        }

        // ============================================================
        // DELETE
        // ============================================================

        public static bool DeleteTest(
            string testId)
        {
            try
            {
                string filePath =
                    GetTestPath(testId);

                if (!File.Exists(filePath))
                    return false;

                File.Delete(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // VALIDATE
        // ============================================================

        public static bool ValidateTest(
            string testId)
        {
            try
            {
                OptimizationBenchmarkResult? test =
                    LoadTest(testId);

                if (test == null)
                    return false;

                if (string.IsNullOrWhiteSpace(
                    test.TestId))
                {
                    return false;
                }

                if (test.StartedAt == default)
                    return false;

                if (string.IsNullOrWhiteSpace(
                    test.OptimizationTitle))
                {
                    return false;
                }

                if (!test.BaselineCompleted)
                    return false;

                if (!test.SnapshotCreated)
                    return false;

                if (!test.OptimizationApplied)
                    return false;

                if (!test.OptimizationVerified)
                    return false;

                if (!test.AfterBenchmarkCompleted)
                    return false;

                if (!test.ComparisonCompleted)
                    return false;

                if (test.Baseline == null)
                    return false;

                if (test.After == null)
                    return false;

                if (test.Comparison == null)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // STORAGE LOCATION
        // ============================================================

        public static string GetTestDirectory()
        {
            Directory.CreateDirectory(
                TestDirectory);

            return TestDirectory;
        }

        // ============================================================
        // FILE PATH
        // ============================================================

        private static string GetTestPath(
            string testId)
        {
            if (string.IsNullOrWhiteSpace(testId))
            {
                throw new ArgumentException(
                    "Invalid optimization test ID.",
                    nameof(testId));
            }

            foreach (char character in testId)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    throw new ArgumentException(
                        "Invalid optimization test ID.",
                        nameof(testId));
                }
            }

            return Path.Combine(
                TestDirectory,
                $"{testId}.json");
        }
    }
}