using System;
using System.Diagnostics;

namespace BuildCore
{
    public class PerformanceData
    {
        public float CpuUsage { get; set; }
        public float RamUsage { get; set; }
        public float DiskUsage { get; set; }
    }

    public class PerformanceService
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;
        private readonly PerformanceCounter _diskCounter;

        public PerformanceService()
        {
            _cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total");

            _ramCounter = new PerformanceCounter(
                "Memory",
                "% Committed Bytes In Use");

            _diskCounter = new PerformanceCounter(
                "PhysicalDisk",
                "% Disk Time",
                "_Total");

            // First call initializes the counters.
            _cpuCounter.NextValue();
            _ramCounter.NextValue();
            _diskCounter.NextValue();
        }

        public PerformanceData GetPerformance()
        {
            return new PerformanceData
            {
                CpuUsage = _cpuCounter.NextValue(),
                RamUsage = _ramCounter.NextValue(),
                DiskUsage = _diskCounter.NextValue()
            };
        }
    }
}