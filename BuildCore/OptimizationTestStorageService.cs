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

                OptimizationBenchmarkResult? test =
                    JsonSerializer.Deserialize<
                        OptimizationBenchmarkResult>(
                            json,
                            JsonOptions);

                return NormalizeLoadedTest(test);
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

                    test = NormalizeLoadedTest(test);

                    if (test == null)
                        continue;

                    tests.Add(test);
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
                OptimizationBenchmarkResult? test = LoadTest(testId);

                if (test == null ||
                    string.IsNullOrWhiteSpace(test.TestId) ||
                    test.StartedAt == default ||
                    string.IsNullOrWhiteSpace(test.OptimizationTitle))
                {
                    return false;
                }

                if (test.CompletedAt != default &&
                    test.CompletedAt < test.StartedAt)
                {
                    return false;
                }

                if (IsWorkloadTest(test))
                    return ValidateWorkloadTest(test);

                return ValidateLegacyTest(test);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWorkloadTest(
            OptimizationBenchmarkResult test)
        {
            return test.WorkloadDefinition != null ||
                   test.WorkloadBaseline != null ||
                   test.WorkloadAfter != null ||
                   test.WorkloadAnalysis != null;
        }

        private static bool ValidateLegacyTest(
            OptimizationBenchmarkResult test)
        {
            return test.BaselineCompleted &&
                   test.SnapshotCreated &&
                   test.OptimizationApplied &&
                   test.OptimizationVerified &&
                   test.AfterBenchmarkCompleted &&
                   test.ComparisonCompleted &&
                   test.AnalysisCompleted &&
                   test.Baseline != null &&
                   test.After != null &&
                   test.Comparison != null &&
                   test.Analysis != null;
        }

        private static bool ValidateWorkloadTest(
            OptimizationBenchmarkResult test)
        {
            BenchmarkWorkload? definition = test.WorkloadDefinition;
            WorkloadBenchmarkResult? baseline = test.WorkloadBaseline;
            WorkloadBenchmarkResult? after = test.WorkloadAfter;
            WorkloadStatisticalAnalysis? analysis = test.WorkloadAnalysis;
            WorkloadEvidenceQuality? evidence = test.WorkloadEvidenceQuality;

            if (definition == null || !definition.IsValid ||
                baseline == null || after == null || analysis == null ||
                evidence == null)
            {
                return false;
            }

            if (!test.WorkloadBaselineCompleted ||
                !test.WorkloadSnapshotCreated ||
                !test.WorkloadOptimizationApplied ||
                !test.WorkloadOptimizationVerified ||
                !test.WorkloadAfterCompleted ||
                !test.WorkloadAnalysisCompleted ||
                !test.WorkloadEvidenceGatePassed)
            {
                return false;
            }

            if (test.TestState != ControlledWorkloadTestState.Completed ||
                !string.Equals(test.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!test.IsSuccessful || !analysis.IsComparable ||
                !evidence.IsSufficient)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(test.WorkloadFingerprint) ||
                !string.Equals(test.WorkloadFingerprint, baseline.WorkloadFingerprint, StringComparison.Ordinal) ||
                !string.Equals(test.WorkloadFingerprint, after.WorkloadFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            if (!baseline.IsComplete || !after.IsComplete ||
                baseline.CompletedRuns < 3 || after.CompletedRuns < 3 ||
                analysis.PairedRunCount < 3)
            {
                return false;
            }

            if (test.WorkloadEnvironmentComparison == null ||
                !test.WorkloadEnvironmentComparison.IsComparable)
            {
                return false;
            }

            return true;
        }

        // ============================================================
        // BACKWARD COMPATIBILITY
        // ============================================================

        private static OptimizationBenchmarkResult? NormalizeLoadedTest(
            OptimizationBenchmarkResult? test)
        {
            if (test == null)
                return null;

            // Older saved tests may not contain fields added in later
            // BuildCore versions. Keep those fields null rather than
            // inventing benchmark data.
            test.OptimizationTitle ??= "";
            test.SnapshotId ??= "";
            test.Status ??= "Unknown";

            return test;
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