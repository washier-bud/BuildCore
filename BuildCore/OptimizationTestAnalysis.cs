using System;

namespace BuildCore
{
    public enum OptimizationTestOutcome
    {
        ImprovementDetected,
        NoMeaningfulChange,
        RegressionDetected,
        Inconclusive
    }

    public class OptimizationTestAnalysis
    {
        public string AnalysisId { get; set; } =
            Guid.NewGuid().ToString("N");

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;

        public OptimizationTestOutcome Outcome { get; set; } =
            OptimizationTestOutcome.Inconclusive;

        public string Summary { get; set; } =
            "";

        // ============================================================
        // CPU
        // ============================================================

        public double CpuBefore { get; set; }

        public double CpuAfter { get; set; }

        public double CpuDelta =>
            CpuAfter - CpuBefore;

        // ============================================================
        // GPU
        // ============================================================

        public double GpuBefore { get; set; }

        public double GpuAfter { get; set; }

        public double GpuDelta =>
            GpuAfter - GpuBefore;

        // ============================================================
        // GPU CLOCK
        // ============================================================

        public double GpuClockBeforeMHz { get; set; }

        public double GpuClockAfterMHz { get; set; }

        public double GpuClockDeltaMHz =>
            GpuClockAfterMHz - GpuClockBeforeMHz;

        // ============================================================
        // GPU TEMPERATURE
        // ============================================================

        public double GpuTemperatureBeforeC { get; set; }

        public double GpuTemperatureAfterC { get; set; }

        public double GpuTemperatureDeltaC =>
            GpuTemperatureAfterC -
            GpuTemperatureBeforeC;

        // ============================================================
        // VRAM
        // ============================================================

        public double VramBeforeGB { get; set; }

        public double VramAfterGB { get; set; }

        public double VramDeltaGB =>
            VramAfterGB - VramBeforeGB;

        // ============================================================
        // VARIANCE
        // ============================================================

        public double CpuStandardDeviationBefore { get; set; }

        public double CpuStandardDeviationAfter { get; set; }

        public double GpuStandardDeviationBefore { get; set; }

        public double GpuStandardDeviationAfter { get; set; }

        public double GpuTemperatureStandardDeviationBefore { get; set; }

        public double GpuTemperatureStandardDeviationAfter { get; set; }

        // ============================================================
        // CONFIDENCE / CONSISTENCY
        // ============================================================

        public bool BaselineReliable { get; set; }

        public bool AfterReliable { get; set; }

        public bool IsComparable { get; set; }

        public double NoiseThresholdPercent { get; set; } =
            2.0;

        public double ConfidenceScore { get; set; }

        // ============================================================
        // FACTORY
        // ============================================================

        public static OptimizationTestAnalysis Analyze(
            ReliableBenchmarkResult baseline,
            ReliableBenchmarkResult after)
        {
            if (baseline == null)
                throw new ArgumentNullException(
                    nameof(baseline));

            if (after == null)
                throw new ArgumentNullException(
                    nameof(after));

            var analysis =
                new OptimizationTestAnalysis
                {
                    AnalysisId =
                        Guid.NewGuid().ToString("N"),

                    CreatedAt =
                        DateTime.Now,

                    BaselineReliable =
                        baseline.IsComplete &&
                        baseline.HasEnoughRuns,

                    AfterReliable =
                        after.IsComplete &&
                        after.HasEnoughRuns,

                    IsComparable =
                        baseline.IsComplete &&
                        after.IsComplete
                };

            analysis.CpuBefore =
                baseline.CpuAverage;

            analysis.CpuAfter =
                after.CpuAverage;

            analysis.GpuBefore =
                baseline.GpuAverage;

            analysis.GpuAfter =
                after.GpuAverage;

            analysis.GpuClockBeforeMHz =
                baseline.GpuClockAverageMHz;

            analysis.GpuClockAfterMHz =
                after.GpuClockAverageMHz;

            analysis.GpuTemperatureBeforeC =
                baseline.GpuTemperatureAverage;

            analysis.GpuTemperatureAfterC =
                after.GpuTemperatureAverage;

            analysis.VramBeforeGB =
                baseline.VramAverageGB;

            analysis.VramAfterGB =
                after.VramAverageGB;

            analysis.CpuStandardDeviationBefore =
                baseline.CpuStandardDeviation;

            analysis.CpuStandardDeviationAfter =
                after.CpuStandardDeviation;

            analysis.GpuStandardDeviationBefore =
                baseline.GpuStandardDeviation;

            analysis.GpuStandardDeviationAfter =
                after.GpuStandardDeviation;

            analysis.GpuTemperatureStandardDeviationBefore =
                baseline.GpuTemperatureStandardDeviation;

            analysis.GpuTemperatureStandardDeviationAfter =
                after.GpuTemperatureStandardDeviation;

            if (!analysis.IsComparable)
            {
                analysis.Outcome =
                    OptimizationTestOutcome.Inconclusive;

                analysis.ConfidenceScore =
                    0;

                analysis.Summary =
                    "The benchmark runs were not complete " +
                    "enough to make a reliable comparison.";

                return analysis;
            }

            analysis.ConfidenceScore =
                CalculateConfidence(
                    baseline,
                    after);

            analysis.Outcome =
                DetermineOutcome(
                    analysis);

            analysis.Summary =
                BuildSummary(
                    analysis);

            return analysis;
        }

        // ============================================================
        // OUTCOME
        // ============================================================

        private static OptimizationTestOutcome
            DetermineOutcome(
                OptimizationTestAnalysis analysis)
        {
            double gpuRelativeChange =
                CalculateRelativeChange(
                    analysis.GpuBefore,
                    analysis.GpuAfter);

            double clockRelativeChange =
                CalculateRelativeChange(
                    analysis.GpuClockBeforeMHz,
                    analysis.GpuClockAfterMHz);

            double temperatureChange =
                analysis.GpuTemperatureDeltaC;

            bool improvement =
                gpuRelativeChange >=
                    analysis.NoiseThresholdPercent
                ||
                clockRelativeChange >=
                    analysis.NoiseThresholdPercent;

            bool regression =
                gpuRelativeChange <=
                    -analysis.NoiseThresholdPercent
                ||
                clockRelativeChange <=
                    -analysis.NoiseThresholdPercent;

            // A large temperature increase is treated as
            // a warning rather than automatic proof of regression.
            bool thermalWarning =
                temperatureChange >= 5.0;

            if (analysis.ConfidenceScore < 50)
            {
                return OptimizationTestOutcome.Inconclusive;
            }

            if (regression)
            {
                return OptimizationTestOutcome.RegressionDetected;
            }

            if (improvement &&
                !thermalWarning)
            {
                return OptimizationTestOutcome.ImprovementDetected;
            }

            if (!improvement &&
                !regression)
            {
                return OptimizationTestOutcome.NoMeaningfulChange;
            }

            return OptimizationTestOutcome.Inconclusive;
        }

        // ============================================================
        // CONFIDENCE
        // ============================================================

        private static double CalculateConfidence(
            ReliableBenchmarkResult baseline,
            ReliableBenchmarkResult after)
        {
            double score = 100;

            if (!baseline.HasEnoughRuns)
                score -= 30;

            if (!after.HasEnoughRuns)
                score -= 30;

            if (baseline.CpuStandardDeviation > 5)
                score -= 10;

            if (after.CpuStandardDeviation > 5)
                score -= 10;

            if (baseline.GpuStandardDeviation > 5)
                score -= 10;

            if (after.GpuStandardDeviation > 5)
                score -= 10;

            if (baseline.GpuTemperatureStandardDeviation > 3)
                score -= 5;

            if (after.GpuTemperatureStandardDeviation > 3)
                score -= 5;

            return Math.Clamp(
                score,
                0,
                100);
        }

        // ============================================================
        // RELATIVE CHANGE
        // ============================================================

        private static double CalculateRelativeChange(
            double before,
            double after)
        {
            if (Math.Abs(before) < 0.000001)
                return 0;

            return
                ((after - before) /
                 Math.Abs(before)) *
                100.0;
        }

        // ============================================================
        // SUMMARY
        // ============================================================

        private static string BuildSummary(
            OptimizationTestAnalysis analysis)
        {
            return analysis.Outcome switch
            {
                OptimizationTestOutcome.ImprovementDetected =>
                    $"Measured telemetry changed beyond the " +
                    $"configured {analysis.NoiseThresholdPercent:F1}% " +
                    $"noise threshold. Further workload-specific " +
                    $"testing is recommended before treating the " +
                    $"optimization as beneficial.",

                OptimizationTestOutcome.RegressionDetected =>
                    $"Measured telemetry changed in a potentially " +
                    $"unfavorable direction beyond the configured " +
                    $"{analysis.NoiseThresholdPercent:F1}% noise threshold.",

                OptimizationTestOutcome.NoMeaningfulChange =>
                    $"The measured change remained within the " +
                    $"configured {analysis.NoiseThresholdPercent:F1}% " +
                    $"noise threshold.",

                _ =>
                    "The benchmark data was too inconsistent or " +
                    "incomplete to make a reliable determination."
            };
        }
    }
}