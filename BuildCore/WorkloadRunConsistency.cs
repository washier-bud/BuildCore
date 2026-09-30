using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public class WorkloadRunConsistency
    {
        public int RunCount { get; set; }
        public double AverageFpsStandardDeviation { get; set; }
        public double AverageFpsCoefficientOfVariationPercent { get; set; }
        public double AverageFrameTimeStandardDeviation { get; set; }
        public double AverageFrameTimeCoefficientOfVariationPercent { get; set; }
        public double MaximumFpsDeviationPercent { get; set; }
        public double MaximumFrameTimeDeviationPercent { get; set; }
        public bool HasFrameTimeData { get; set; }
        public string Status { get; set; } = "Telemetry Only";
        public string Summary { get; set; } = "";

        public static WorkloadRunConsistency Calculate(IEnumerable<BenchmarkRun> runs)
        {
            BenchmarkRun[] validRuns = runs?.Where(r => r != null && r.IsSuccessful).ToArray()
                ?? Array.Empty<BenchmarkRun>();

            var result = new WorkloadRunConsistency { RunCount = validRuns.Length };

            double[] fps = validRuns
                .Select(r => r.FrameTime?.AverageFps)
                .Where(v => v.HasValue && IsFinite(v.Value) && v.Value > 0)
                .Select(v => v!.Value).ToArray();

            double[] frameTime = validRuns
                .Select(r => r.FrameTime?.AverageFrameTimeMilliseconds)
                .Where(v => v.HasValue && IsFinite(v.Value) && v.Value > 0)
                .Select(v => v!.Value).ToArray();

            if (fps.Length > 0)
            {
                result.HasFrameTimeData = true;
                result.AverageFpsStandardDeviation = StandardDeviation(fps);
                result.AverageFpsCoefficientOfVariationPercent =
                    RelativeSd(result.AverageFpsStandardDeviation, fps.Average());
                result.MaximumFpsDeviationPercent = MaximumDeviation(fps);
            }

            if (frameTime.Length > 0)
            {
                result.HasFrameTimeData = true;
                result.AverageFrameTimeStandardDeviation = StandardDeviation(frameTime);
                result.AverageFrameTimeCoefficientOfVariationPercent =
                    RelativeSd(result.AverageFrameTimeStandardDeviation, frameTime.Average());
                result.MaximumFrameTimeDeviationPercent = MaximumDeviation(frameTime);
            }

            result.Status = result.HasFrameTimeData ? "Frame-Time Captured" : "Telemetry Only";
            result.Summary = result.HasFrameTimeData
                ? $"Run-to-run consistency calculated across {validRuns.Length} successful runs. " +
                  $"Average FPS variation: {result.AverageFpsCoefficientOfVariationPercent:F2}%. " +
                  $"Average frame-time variation: {result.AverageFrameTimeCoefficientOfVariationPercent:F2}%."
                : $"Run-to-run telemetry consistency recorded across {validRuns.Length} successful runs.";

            return result;
        }

        private static double RelativeSd(double sd, double average) =>
            Math.Abs(average) < 0.000001 ? 0 : sd / Math.Abs(average) * 100.0;

        private static double MaximumDeviation(double[] values)
        {
            if (values.Length == 0) return 0;
            double average = values.Average();
            if (Math.Abs(average) < 0.000001) return 0;
            return values.Max(v => Math.Abs(v - average) / Math.Abs(average) * 100.0);
        }

        private static double StandardDeviation(double[] values)
        {
            if (values.Length <= 1) return 0;
            double average = values.Average();
            return Math.Sqrt(values.Select(v => Math.Pow(v - average, 2)).Average());
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}