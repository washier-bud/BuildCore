using System;

namespace BuildCore
{
    public static class BenchmarkComparisonService
    {
        public static BenchmarkComparison Compare(
            string optimizationTitle,
            BenchmarkResult baseline,
            BenchmarkResult after)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(
                    nameof(baseline));
            }

            if (after == null)
            {
                throw new ArgumentNullException(
                    nameof(after));
            }

            ValidateBenchmark(
                baseline,
                "baseline");

            ValidateBenchmark(
                after,
                "after");

            return BenchmarkComparison.Create(
                optimizationTitle,
                baseline,
                after);
        }

        private static void ValidateBenchmark(
            BenchmarkResult benchmark,
            string name)
        {
            if (string.IsNullOrWhiteSpace(
                benchmark.BenchmarkId))
            {
                throw new InvalidOperationException(
                    $"The {name} benchmark has no ID.");
            }

            if (!benchmark.Status.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The {name} benchmark did not complete successfully.");
            }

            if (benchmark.SampleCount <= 0)
            {
                throw new InvalidOperationException(
                    $"The {name} benchmark contains no samples.");
            }

            if (benchmark.DurationSeconds <= 0)
            {
                throw new InvalidOperationException(
                    $"The {name} benchmark has an invalid duration.");
            }
        }

        public static bool
            AreComparable(
                BenchmarkResult baseline,
                BenchmarkResult after)
        {
            if (baseline == null ||
                after == null)
            {
                return false;
            }

            if (!baseline.Status.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!after.Status.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (baseline.SampleCount <= 0 ||
                after.SampleCount <= 0)
            {
                return false;
            }

            if (baseline.DurationSeconds <= 0 ||
                after.DurationSeconds <= 0)
            {
                return false;
            }

            return true;
        }
    }
}