using System;

namespace BuildCore
{
    public static class OptimizationResultsFormatter
    {
        // ------------------------------------------------------------
        // STATUS
        // ------------------------------------------------------------

        public static string BuildStatus(
            OptimizationBenchmarkResult? result)
        {
            if (result == null)
            {
                return "INCONCLUSIVE";
            }

            if (result.Analysis != null)
            {
                return FormatOutcome(
                    result.Analysis);
            }

            if (!result.BaselineCompleted)
            {
                return "BASELINE FAILED";
            }

            if (!result.SnapshotCreated)
            {
                return "SNAPSHOT FAILED";
            }

            if (!result.OptimizationApplied ||
                !result.OptimizationVerified)
            {
                return "OPTIMIZATION FAILED";
            }

            if (!result.AfterBenchmarkCompleted)
            {
                return "AFTER TEST FAILED";
            }

            if (!result.ComparisonCompleted)
            {
                return "COMPARISON INCOMPLETE";
            }

            return result.Status.ToUpperInvariant();
        }

        // ------------------------------------------------------------
        // SUMMARY
        // ------------------------------------------------------------

        public static string BuildSummary(
            OptimizationBenchmarkResult? result)
        {
            if (result == null)
            {
                return
                    "No optimization test result is available.";
            }

            if (result.Analysis != null)
            {
                return result.Analysis.Summary;
            }

            if (!string.IsNullOrWhiteSpace(
                result.Status))
            {
                return result.Status;
            }

            return
                "No optimization test summary is available.";
        }

        // ------------------------------------------------------------
        // OUTCOME
        // ------------------------------------------------------------

        public static string FormatOutcome(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "INCONCLUSIVE";
            }

            return analysis.Outcome switch
            {
                OptimizationTestOutcome.ImprovementDetected =>
                    "IMPROVEMENT DETECTED",

                OptimizationTestOutcome.NoMeaningfulChange =>
                    "NO MEANINGFUL CHANGE",

                OptimizationTestOutcome.RegressionDetected =>
                    "REGRESSION DETECTED",

                _ =>
                    "INCONCLUSIVE"
            };
        }

        // ------------------------------------------------------------
        // PERCENTAGES
        // ------------------------------------------------------------

        public static string FormatPercentage(
            double value)
        {
            return $"{value:F1}%";
        }

        public static string FormatPercentageDelta(
            double value)
        {
            if (Math.Abs(value) < 0.01)
            {
                return "0.0 pp";
            }

            return
                $"{value:+0.0;-0.0;0.0} pp";
        }

        // ------------------------------------------------------------
        // MEGAHERTZ
        // ------------------------------------------------------------

        public static string FormatMegahertz(
            double value)
        {
            return $"{value:F0} MHz";
        }

        public static string FormatMegahertzDelta(
            double value)
        {
            if (Math.Abs(value) < 0.01)
            {
                return "0 MHz";
            }

            return
                $"{value:+0;-0;0} MHz";
        }

        // ------------------------------------------------------------
        // TEMPERATURE
        // ------------------------------------------------------------

        public static string FormatTemperature(
            double value)
        {
            return $"{value:F1} °C";
        }

        public static string FormatTemperatureDelta(
            double value)
        {
            if (Math.Abs(value) < 0.01)
            {
                return "0.0 °C";
            }

            return
                $"{value:+0.0;-0.0;0.0} °C";
        }

        // ------------------------------------------------------------
        // GIGABYTES
        // ------------------------------------------------------------

        public static string FormatGigabytes(
            double value)
        {
            return $"{value:F2} GB";
        }

        public static string FormatGigabyteDelta(
            double value)
        {
            if (Math.Abs(value) < 0.005)
            {
                return "0.00 GB";
            }

            return
                $"{value:+0.00;-0.00;0.00} GB";
        }

        // ------------------------------------------------------------
        // DURATION
        // ------------------------------------------------------------

        public static string FormatDuration(
            double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            if (seconds < 60)
            {
                return $"{seconds:F2}s";
            }

            TimeSpan duration =
                TimeSpan.FromSeconds(seconds);

            return
                $"{duration.Minutes}m " +
                $"{duration.Seconds}s";
        }

        // ------------------------------------------------------------
        // CONFIDENCE
        // ------------------------------------------------------------

        public static string FormatConfidence(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "Confidence: 0%";
            }

            return
                $"Confidence: " +
                $"{analysis.ConfidenceScore:F0}%";
        }

        // ------------------------------------------------------------
        // RELIABILITY
        // ------------------------------------------------------------

        public static string FormatReliability(
            ReliableBenchmarkResult? result)
        {
            if (result == null)
            {
                return "UNKNOWN";
            }

            return
                result.ReliabilityStatus
                    .ToUpperInvariant();
        }

        public static string FormatRunCount(
            ReliableBenchmarkResult? result)
        {
            if (result == null)
            {
                return "0 runs";
            }

            return
                $"{result.CompletedRuns}/" +
                $"{result.RequestedRuns} runs";
        }

        // ------------------------------------------------------------
        // CPU
        // ------------------------------------------------------------

        public static string FormatCpuComparison(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "CPU data unavailable";
            }

            return
                $"{FormatPercentage(analysis.CpuBefore)} → " +
                $"{FormatPercentage(analysis.CpuAfter)} " +
                $"({FormatPercentageDelta(analysis.CpuDelta)})";
        }

        // ------------------------------------------------------------
        // GPU
        // ------------------------------------------------------------

        public static string FormatGpuComparison(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "GPU data unavailable";
            }

            return
                $"{FormatPercentage(analysis.GpuBefore)} → " +
                $"{FormatPercentage(analysis.GpuAfter)} " +
                $"({FormatPercentageDelta(analysis.GpuDelta)})";
        }

        // ------------------------------------------------------------
        // GPU CLOCK
        // ------------------------------------------------------------

        public static string FormatGpuClockComparison(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "GPU clock unavailable";
            }

            return
                $"{FormatMegahertz(analysis.GpuClockBeforeMHz)} → " +
                $"{FormatMegahertz(analysis.GpuClockAfterMHz)} " +
                $"({FormatMegahertzDelta(analysis.GpuClockDeltaMHz)})";
        }

        // ------------------------------------------------------------
        // GPU TEMPERATURE
        // ------------------------------------------------------------

        public static string FormatGpuTemperatureComparison(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "GPU temperature unavailable";
            }

            return
                $"{FormatTemperature(analysis.GpuTemperatureBeforeC)} → " +
                $"{FormatTemperature(analysis.GpuTemperatureAfterC)} " +
                $"({FormatTemperatureDelta(analysis.GpuTemperatureDeltaC)})";
        }

        // ------------------------------------------------------------
        // VRAM
        // ------------------------------------------------------------

        public static string FormatVramComparison(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return "VRAM data unavailable";
            }

            return
                $"{FormatGigabytes(analysis.VramBeforeGB)} → " +
                $"{FormatGigabytes(analysis.VramAfterGB)} " +
                $"({FormatGigabyteDelta(analysis.VramDeltaGB)})";
        }

        // ------------------------------------------------------------
        // STANDARD DEVIATION
        // ------------------------------------------------------------

        public static string FormatStandardDeviation(
            double value,
            string suffix = "")
        {
            return
                $"±{value:F2}{suffix}";
        }

        // ------------------------------------------------------------
        // ANALYSIS SUMMARY
        // ------------------------------------------------------------

        public static string FormatAnalysisSummary(
            OptimizationTestAnalysis? analysis)
        {
            if (analysis == null)
            {
                return
                    "No statistical analysis is available.";
            }

            return analysis.Summary;
        }

        // ------------------------------------------------------------
        // LEGACY GENERIC DELTA FORMAT
        // ------------------------------------------------------------

        public static string FormatDelta(
            double delta,
            string suffix = "")
        {
            if (Math.Abs(delta) < 0.01)
            {
                return $"0.00{suffix}";
            }

            return
                $"{delta:+0.00;-0.00;0.00}{suffix}";
        }
    }
}