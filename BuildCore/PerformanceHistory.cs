using System.Collections.Generic;

namespace BuildCore
{
    public class PerformanceHistory
    {
        public List<double> Cpu { get; } = new();
        public List<double> Ram { get; } = new();
        public List<double> Disk { get; } = new();

        private const int MaxSamples = 60;

        public void Add(double cpu, double ram, double disk)
        {
            Cpu.Add(cpu);
            Ram.Add(ram);
            Disk.Add(disk);

            if (Cpu.Count > MaxSamples)
                Cpu.RemoveAt(0);

            if (Ram.Count > MaxSamples)
                Ram.RemoveAt(0);

            if (Disk.Count > MaxSamples)
                Disk.RemoveAt(0);
        }
    }
}