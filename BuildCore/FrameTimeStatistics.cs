using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public class FrameTimeStatistics
    {
        public int SampleCount { get; set; }

        public double AverageFrameTimeMilliseconds { get; set; }

        public double OnePercentLowFrameTimeMilliseconds { get; set; }

        public double ZeroPointOnePercentLowFrameTimeMilliseconds { get; set; }

        public double FrameTimeStandardDeviationMilliseconds { get; set; }

        public double FrameTimeVarianceMillisecondsSquared { get; set; }

        public double AverageFps =>
            AverageFrameTimeMilliseconds > 0
                ? 1000.0 / AverageFrameTimeMilliseconds
                : 0;

        public double OnePercentLowFps =>
            OnePercentLowFrameTimeMilliseconds > 0
                ? 1000.0 / OnePercentLowFrameTimeMilliseconds
                : 0;

        public double ZeroPointOnePercentLowFps =>
            ZeroPointOnePercentLowFrameTimeMilliseconds > 0
                ? 1000.0 / ZeroPointOnePercentLowFrameTimeMilliseconds
                : 0;

        public bool HasData => SampleCount > 0;

        public static FrameTimeStatistics Calculate(
            IEnumerable<FrameTimeSample> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            double[] values = samples
                .Where(sample => sample != null && sample.IsValid)
                .Select(sample => sample.FrameTimeMilliseconds)
                .OrderByDescending(value => value)
                .ToArray();

            if (values.Length == 0)
                return new FrameTimeStatistics();

            double average = values.Average();

            double variance = values
                .Select(value => Math.Pow(value - average, 2))
                .Average();

            int onePercentCount =
                Math.Max(1, (int)Math.Ceiling(values.Length * 0.01));

            int zeroPointOnePercentCount =
                Math.Max(1, (int)Math.Ceiling(values.Length * 0.001));

            double onePercentLowFrameTime =
                values
                    .Take(onePercentCount)
                    .Average();

            double zeroPointOnePercentLowFrameTime =
                values
                    .Take(zeroPointOnePercentCount)
                    .Average();

            return new FrameTimeStatistics
            {
                SampleCount = values.Length,
                AverageFrameTimeMilliseconds = average,
                OnePercentLowFrameTimeMilliseconds =
                    onePercentLowFrameTime,
                ZeroPointOnePercentLowFrameTimeMilliseconds =
                    zeroPointOnePercentLowFrameTime,
                FrameTimeVarianceMillisecondsSquared = variance,
                FrameTimeStandardDeviationMilliseconds =
                    Math.Sqrt(variance)
            };
        }
    }
}
