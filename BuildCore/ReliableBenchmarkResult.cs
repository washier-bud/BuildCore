using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public class ReliableBenchmarkResult
    {
        public string ResultId { get; set; } =
            Guid.NewGuid().ToString("N");

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } =
            "Not Started";

        public string Phase { get; set; } =
            "";

        public int RequestedRuns { get; set; }

        public int CompletedRuns { get; set; }

        public List<BenchmarkRun> Runs { get; set; } =
            new List<BenchmarkRun>();

        // ============================================================
        // CPU
        // ============================================================

        public double CpuAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.CpuAverageUsage);
            }
        }

        public double CpuPeakAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.CpuPeakUsage);
            }
        }

        public double CpuVariance
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.CpuAverageUsage));
            }
        }

        // ============================================================
        // RAM
        // ============================================================

        public double RamAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.RamAverageUsage);
            }
        }

        public double RamPeakAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.RamPeakUsage);
            }
        }

        public double RamVariance
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.RamAverageUsage));
            }
        }

        // ============================================================
        // DISK
        // ============================================================

        public double DiskAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.DiskAverageActivity);
            }
        }

        public double DiskPeakAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.DiskPeakActivity);
            }
        }

        public double DiskVariance
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.DiskAverageActivity));
            }
        }

        // ============================================================
        // GPU
        // ============================================================

        public double GpuAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuAverageUsage);
            }
        }

        public double GpuPeakAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuPeakUsage);
            }
        }

        public double GpuVariance
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.GpuAverageUsage));
            }
        }

        public double GpuClockAverageMHz
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuAverageClockMHz);
            }
        }

        public double GpuClockVarianceMHz
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.GpuAverageClockMHz));
            }
        }

        // ============================================================
        // GPU TEMPERATURE
        // ============================================================

        public double GpuTemperatureAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuAverageTemperature);
            }
        }

        public double GpuTemperaturePeakAverage
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuPeakTemperature);
            }
        }

        public double GpuTemperatureVariance
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.GpuAverageTemperature));
            }
        }

        // ============================================================
        // VRAM
        // ============================================================

        public double VramAverageGB
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuAverageMemoryUsedGB);
            }
        }

        public double VramPeakAverageGB
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.GpuPeakMemoryUsedGB);
            }
        }

        public double VramVarianceGB
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.GpuAverageMemoryUsedGB));
            }
        }

        // ============================================================
        // TIMING
        // ============================================================

        public double DurationAverageSeconds
        {
            get
            {
                return Runs.Count == 0
                    ? 0
                    : Runs.Average(
                        run => run.DurationSeconds);
            }
        }

        public double DurationVarianceSeconds
        {
            get
            {
                return CalculateVariance(
                    Runs.Select(
                        run => run.DurationSeconds));
            }
        }

        // ============================================================
        // RELIABILITY
        // ============================================================

        public bool IsComplete
        {
            get
            {
                return
                    RequestedRuns > 0 &&
                    CompletedRuns >= RequestedRuns &&
                    Runs.Count >= RequestedRuns &&
                    Runs.All(
                        run => run.IsSuccessful);
            }
        }

        public bool HasEnoughRuns
        {
            get
            {
                return CompletedRuns >= 3;
            }
        }

        public string ReliabilityStatus
        {
            get
            {
                if (!IsComplete)
                    return "Incomplete";

                if (!HasEnoughRuns)
                    return "Limited Data";

                return "Reliable";
            }
        }

        // ============================================================
        // STANDARD DEVIATION
        // ============================================================

        public double CpuStandardDeviation =>
            Math.Sqrt(CpuVariance);

        public double RamStandardDeviation =>
            Math.Sqrt(RamVariance);

        public double DiskStandardDeviation =>
            Math.Sqrt(DiskVariance);

        public double GpuStandardDeviation =>
            Math.Sqrt(GpuVariance);

        public double GpuClockStandardDeviationMHz =>
            Math.Sqrt(GpuClockVarianceMHz);

        public double GpuTemperatureStandardDeviation =>
            Math.Sqrt(GpuTemperatureVariance);

        public double VramStandardDeviationGB =>
            Math.Sqrt(VramVarianceGB);

        // ============================================================
        // HELPERS
        // ============================================================

        private static double CalculateVariance(
            IEnumerable<double> values)
        {
            double[] samples =
                values.ToArray();

            if (samples.Length <= 1)
                return 0;

            double average =
                samples.Average();

            double total =
                samples.Sum(
                    value =>
                    Math.Pow(
                        value - average,
                        2));

            return total /
                   samples.Length;
        }
    }
}