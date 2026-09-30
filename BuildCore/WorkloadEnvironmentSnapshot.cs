using System;

namespace BuildCore
{
    public class WorkloadEnvironmentSnapshot
    {
        public DateTime CapturedAt { get; set; }
        public string WindowsVersion { get; set; } = "Unknown";
        public string WindowsBuild { get; set; } = "Unknown";
        public string PowerPlan { get; set; } = "Unknown";
        public bool GameModeEnabled { get; set; }
        public bool HagsEnabled { get; set; }
        public bool MemoryIntegrityEnabled { get; set; }
        public int ProcessorCount { get; set; }
        public int LogicalProcessorCount { get; set; }

        public static WorkloadEnvironmentSnapshot Capture()
        {
            WindowsSystemData data = WindowsSystemService.Scan();

            int processorCount = int.TryParse(data.ProcessorCount, out int cores)
                ? cores : 0;
            int logicalProcessorCount = int.TryParse(
                data.LogicalProcessorCount, out int logical)
                ? logical : 0;

            return new WorkloadEnvironmentSnapshot
            {
                CapturedAt = DateTime.Now,
                WindowsVersion = data.WindowsVersion,
                WindowsBuild = data.WindowsBuild,
                PowerPlan = data.PowerPlan,
                GameModeEnabled = data.GameModeEnabled,
                HagsEnabled = data.HagsEnabled,
                MemoryIntegrityEnabled = data.MemoryIntegrityEnabled,
                ProcessorCount = processorCount,
                LogicalProcessorCount = logicalProcessorCount
            };
        }
    }
}