using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public enum WorkloadAnalysisOutcome
    {
        ImprovementDetected,
        NoMeaningfulChange,
        RegressionDetected,
        Inconclusive
    }

    public class WorkloadMetricAnalysis
    {
        public string Name { get; set; } = "";
        public double Before { get; set; }
        public double After { get; set; }
        public double Delta => After - Before;
        public double RelativeChangePercent { get; set; }
        public double BeforeStandardDeviation { get; set; }
        public double AfterStandardDeviation { get; set; }
        public double NoiseThresholdPercent { get; set; } = 2.0;

        public int BeforeSampleCount { get; set; }
        public int AfterSampleCount { get; set; }
        public double PooledStandardDeviation { get; set; }
        public double SignalToNoiseRatio { get; set; }

        public bool ChangeExceedsNoise =>
            Math.Abs(RelativeChangePercent) >= NoiseThresholdPercent;
    }

    public class WorkloadStatisticalAnalysis
    {
        public string AnalysisId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public WorkloadAnalysisOutcome Outcome { get; set; } =
            WorkloadAnalysisOutcome.Inconclusive;

        public string Summary { get; set; } = "";
        public double ConfidenceScore { get; set; }
        public double NoiseThresholdPercent { get; set; } = 2.0;

        public bool BaselineReliable { get; set; }
        public bool AfterReliable { get; set; }
        public bool IsComparable { get; set; }

        public WorkloadMetricAnalysis? Cpu { get; set; }
        public WorkloadMetricAnalysis? Gpu { get; set; }
        public WorkloadMetricAnalysis? GpuClock { get; set; }
        public WorkloadMetricAnalysis? GpuTemperature { get; set; }
        public WorkloadMetricAnalysis? Vram { get; set; }
        public WorkloadMetricAnalysis? AverageFps { get; set; }
        public WorkloadMetricAnalysis? OnePercentLowFps { get; set; }
        public WorkloadMetricAnalysis? ZeroPointOnePercentLowFps { get; set; }
        public WorkloadMetricAnalysis? AverageFrameTime { get; set; }
        public WorkloadMetricAnalysis? FrameTimeStandardDeviation { get; set; }

        public static WorkloadStatisticalAnalysis Analyze(
            WorkloadBenchmarkResult baseline,
            WorkloadBenchmarkResult after)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var analysis = new WorkloadStatisticalAnalysis
            {
                BaselineReliable = baseline.IsComplete && baseline.HasEnoughRuns,
                AfterReliable = after.IsComplete && after.HasEnoughRuns,
                IsComparable =
                    baseline.IsComplete &&
                    after.IsComplete &&
                    baseline.WorkloadType == after.WorkloadType &&
                    string.Equals(
                        baseline.WorkloadId,
                        after.WorkloadId,
                        StringComparison.OrdinalIgnoreCase)
            };

            if (!analysis.IsComparable)
            {
                analysis.Summary =
                    "The baseline and after-workload results are incomplete " +
                    "or do not represent the same workload.";
                return analysis;
            }

            analysis.Cpu = CreateMetric(
                "CPU utilization",
                baseline.Runs.Select(r => r.CpuAverageUsage),
                after.Runs.Select(r => r.CpuAverageUsage),
                analysis.NoiseThresholdPercent);

            analysis.Gpu = CreateMetric(
                "GPU utilization",
                baseline.Runs.Select(r => r.GpuAverageUsage),
                after.Runs.Select(r => r.GpuAverageUsage),
                analysis.NoiseThresholdPercent);

            analysis.GpuClock = CreateMetric(
                "GPU clock",
                baseline.Runs.Select(r => r.GpuAverageClockMHz),
                after.Runs.Select(r => r.GpuAverageClockMHz),
                analysis.NoiseThresholdPercent);

            analysis.GpuTemperature = CreateMetric(
                "GPU temperature",
                baseline.Runs.Select(r => r.GpuAverageTemperature),
                after.Runs.Select(r => r.GpuAverageTemperature),
                analysis.NoiseThresholdPercent);

            analysis.Vram = CreateMetric(
                "VRAM usage",
                baseline.Runs.Select(r => r.GpuAverageMemoryUsedGB),
                after.Runs.Select(r => r.GpuAverageMemoryUsedGB),
                analysis.NoiseThresholdPercent);

            analysis.AverageFps = CreateFrameTimeMetric(
                "Average FPS",
                baseline.Runs,
                after.Runs,
                r => r.FrameTime?.AverageFps,
                analysis.NoiseThresholdPercent);

            analysis.OnePercentLowFps = CreateFrameTimeMetric(
                "1% low FPS",
                baseline.Runs,
                after.Runs,
                r => r.FrameTime?.OnePercentLowFps,
                analysis.NoiseThresholdPercent);

            analysis.ZeroPointOnePercentLowFps = CreateFrameTimeMetric(
                "0.1% low FPS",
                baseline.Runs,
                after.Runs,
                r => r.FrameTime?.ZeroPointOnePercentLowFps,
                analysis.NoiseThresholdPercent);

            analysis.AverageFrameTime = CreateFrameTimeMetric(
                "Average frame time",
                baseline.Runs,
                after.Runs,
                r => r.FrameTime?.AverageFrameTimeMilliseconds,
                analysis.NoiseThresholdPercent);

            analysis.FrameTimeStandardDeviation = CreateFrameTimeMetric(
                "Frame-time standard deviation",
                baseline.Runs,
                after.Runs,
                r => r.FrameTime?.FrameTimeStandardDeviationMilliseconds,
                analysis.NoiseThresholdPercent);

            analysis.ConfidenceScore = CalculateConfidence(baseline, after, analysis);
            analysis.Outcome = DetermineOutcome(analysis);
            analysis.Summary = BuildSummary(analysis);

            return analysis;
        }

        private static WorkloadMetricAnalysis CreateMetric(
            string name,
            IEnumerable<double> beforeValues,
            IEnumerable<double> afterValues,
            double threshold)
        {
            double[] before = beforeValues.Where(IsFinite).ToArray();
            double[] after = afterValues.Where(IsFinite).ToArray();

            double beforeAverage = before.Length == 0 ? 0 : before.Average();
            double afterAverage = after.Length == 0 ? 0 : after.Average();

            double beforeSd = StandardDeviation(before);
            double afterSd = StandardDeviation(after);
            double pooledSd = CalculatePooledStandardDeviation(
                before, after, beforeSd, afterSd);

            double signal = Math.Abs(afterAverage - beforeAverage);
            double signalToNoise =
                pooledSd > 0 ? signal / pooledSd : (signal > 0 ? 1_000_000 : 0);

            return new WorkloadMetricAnalysis
            {
                Name = name,
                Before = beforeAverage,
                After = afterAverage,
                RelativeChangePercent = CalculateRelativeChange(beforeAverage, afterAverage),
                BeforeStandardDeviation = beforeSd,
                AfterStandardDeviation = afterSd,
                PooledStandardDeviation = pooledSd,
                SignalToNoiseRatio = signalToNoise,
                BeforeSampleCount = before.Length,
                AfterSampleCount = after.Length,
                NoiseThresholdPercent = threshold
            };
        }

        private static WorkloadMetricAnalysis? CreateFrameTimeMetric(
            string name,
            IEnumerable<BenchmarkRun> baselineRuns,
            IEnumerable<BenchmarkRun> afterRuns,
            Func<BenchmarkRun, double?> selector,
            double threshold)
        {
            double[] before = baselineRuns
                .Select(selector)
                .Where(v => v.HasValue && IsFinite(v.Value))
                .Select(v => v!.Value)
                .ToArray();

            double[] after = afterRuns
                .Select(selector)
                .Where(v => v.HasValue && IsFinite(v.Value))
                .Select(v => v!.Value)
                .ToArray();

            if (before.Length == 0 || after.Length == 0)
                return null;

            return CreateMetric(name, before, after, threshold);
        }

        private static double CalculateConfidence(
            WorkloadBenchmarkResult baseline,
            WorkloadBenchmarkResult after,
            WorkloadStatisticalAnalysis analysis)
        {
            double score = 100;

            if (!analysis.BaselineReliable) score -= 25;
            if (!analysis.AfterReliable) score -= 25;

            foreach (var metric in new[]
            {
                analysis.Cpu,
                analysis.Gpu,
                analysis.GpuClock,
                analysis.GpuTemperature,
                analysis.AverageFps,
                analysis.OnePercentLowFps,
                analysis.ZeroPointOnePercentLowFps,
                analysis.AverageFrameTime
            })
            {
                if (metric == null) continue;

                double scale = Math.Max(Math.Abs(metric.Before), 0.000001);
                double relativeNoise =
                    metric.PooledStandardDeviation / scale * 100.0;

                if (relativeNoise > 5) score -= 5;
                if (relativeNoise > 10) score -= 10;

                if (metric.SignalToNoiseRatio < 1)
                    score -= 5;
            }

            return Math.Clamp(score, 0, 100);
        }

        private static WorkloadAnalysisOutcome DetermineOutcome(
            WorkloadStatisticalAnalysis analysis)
        {
            if (analysis.ConfidenceScore < 50)
                return WorkloadAnalysisOutcome.Inconclusive;

            // Without a real workload performance metric such as FPS or
            // frame time, telemetry alone cannot establish an improvement
            // or regression in workload performance.
            bool hasPerformanceMetric =
                analysis.AverageFps != null ||
                analysis.OnePercentLowFps != null ||
                analysis.ZeroPointOnePercentLowFps != null ||
                analysis.AverageFrameTime != null ||
                analysis.FrameTimeStandardDeviation != null;

            if (!hasPerformanceMetric)
                return WorkloadAnalysisOutcome.Inconclusive;

            // For performance metrics, higher is generally better.
            bool performanceImproved =
                ExceedsPositiveChange(analysis.AverageFps) ||
                ExceedsPositiveChange(analysis.OnePercentLowFps) ||
                ExceedsPositiveChange(analysis.ZeroPointOnePercentLowFps);

            bool performanceRegressed =
                ExceedsNegativeChange(analysis.AverageFps) ||
                ExceedsNegativeChange(analysis.OnePercentLowFps) ||
                ExceedsNegativeChange(analysis.ZeroPointOnePercentLowFps);

            // For frame time, lower is generally better.
            performanceImproved |= ExceedsNegativeChange(analysis.AverageFrameTime);
            performanceImproved |= ExceedsNegativeChange(analysis.FrameTimeStandardDeviation);

            performanceRegressed |= ExceedsPositiveChange(analysis.AverageFrameTime);
            performanceRegressed |= ExceedsPositiveChange(analysis.FrameTimeStandardDeviation);

            if (performanceImproved && performanceRegressed)
                return WorkloadAnalysisOutcome.Inconclusive;

            if (performanceImproved)
                return WorkloadAnalysisOutcome.ImprovementDetected;

            if (performanceRegressed)
                return WorkloadAnalysisOutcome.RegressionDetected;

            return WorkloadAnalysisOutcome.NoMeaningfulChange;
        }

        private static bool ExceedsPositiveChange(WorkloadMetricAnalysis? metric) =>
            metric != null && metric.RelativeChangePercent >= metric.NoiseThresholdPercent;

        private static bool ExceedsNegativeChange(WorkloadMetricAnalysis? metric) =>
            metric != null && metric.RelativeChangePercent <= -metric.NoiseThresholdPercent;

        private static string BuildSummary(WorkloadStatisticalAnalysis analysis)
        {
            return analysis.Outcome switch
            {
                WorkloadAnalysisOutcome.ImprovementDetected =>
                    "The workload measurements show a change beyond the configured noise threshold. " +
                    "This is workload evidence, not proof that every application will improve.",

                WorkloadAnalysisOutcome.RegressionDetected =>
                    "The workload measurements show a potentially unfavorable change beyond the configured noise threshold.",

                WorkloadAnalysisOutcome.NoMeaningfulChange =>
                    "The measured performance metrics remained within the configured noise threshold.",

                _ =>
                    "The workload data is too inconsistent, incomplete, or conflicting to make a reliable determination."
            };
        }

        private static double CalculateRelativeChange(double before, double after)
        {
            if (Math.Abs(before) < 0.000001)
                return 0;

            return (after - before) / Math.Abs(before) * 100.0;
        }

        private static double StandardDeviation(double[] values)
        {
            if (values.Length <= 1) return 0;
            double average = values.Average();
            double variance = values.Select(v => Math.Pow(v - average, 2)).Average();
            return Math.Sqrt(variance);
        }

        private static double CalculatePooledStandardDeviation(
            double[] before,
            double[] after,
            double beforeSd,
            double afterSd)
        {
            int n1 = before.Length;
            int n2 = after.Length;

            if (n1 + n2 <= 2)
                return Math.Max(beforeSd, afterSd);

            double pooledVariance =
                (((n1 - 1) * beforeSd * beforeSd) +
                 ((n2 - 1) * afterSd * afterSd)) /
                (n1 + n2 - 2);

            return Math.Sqrt(Math.Max(0, pooledVariance));
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
