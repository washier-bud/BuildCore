using System.Collections.Generic;

namespace BuildCore
{
    public static class BenchmarkWorkloadProfiles
    {
        public static IReadOnlyList<BenchmarkWorkloadProfile> GetAll()
        {
            return new List<BenchmarkWorkloadProfile>
            {
                Create(
                    "Gaming",
                    BenchmarkWorkloadType.Gaming,
                    "Generic interactive gaming workload profile. " +
                    "Uses PresentMon when a target process is selected.",
                    true,
                    false),

                Create(
                    "CPU",
                    BenchmarkWorkloadType.Cpu,
                    "CPU-focused workload profile for processor testing.",
                    false,
                    false),

                Create(
                    "GPU",
                    BenchmarkWorkloadType.Gpu,
                    "GPU-focused workload profile for graphics testing.",
                    false,
                    false),

                Create(
                    "Memory",
                    BenchmarkWorkloadType.Memory,
                    "Memory-focused workload profile for RAM testing.",
                    false,
                    false),

                Create(
                    "Storage",
                    BenchmarkWorkloadType.Storage,
                    "Storage-focused workload profile for disk testing.",
                    false,
                    false),

                Create(
                    "Productivity",
                    BenchmarkWorkloadType.Productivity,
                    "General productivity workload profile.",
                    false,
                    false),

                Create(
                    "Custom",
                    BenchmarkWorkloadType.Custom,
                    "User-defined workload profile.",
                    false,
                    false)
            };
        }

        public static BenchmarkWorkloadProfile CreateTelemetryProfile(
            BenchmarkWorkloadType type,
            string name,
            string description)
        {
            return new BenchmarkWorkloadProfile
            {
                Definition = new BenchmarkWorkload
                {
                    Name = name,
                    Type = type,
                    Description = description,
                    DurationSeconds = 5,
                    RunCount = 3,
                    SampleIntervalMilliseconds = 100,
                    DelayBetweenRunsMilliseconds = 500,
                    RequiresInteractiveWorkload = false
                },
                RecommendedUse =
                    "Telemetry-only baseline. This does not claim to " +
                    "represent real application performance.",
                UsesRealFrameTimeSource = false,
                IsTelemetryOnly = true
            };
        }

        private static BenchmarkWorkloadProfile Create(
            string name,
            BenchmarkWorkloadType type,
            string description,
            bool interactive,
            bool telemetryOnly)
        {
            return new BenchmarkWorkloadProfile
            {
                Definition = new BenchmarkWorkload
                {
                    Name = name,
                    Type = type,
                    Description = description,
                    DurationSeconds = 5,
                    RunCount = 3,
                    SampleIntervalMilliseconds = 100,
                    DelayBetweenRunsMilliseconds = 500,
                    RequiresInteractiveWorkload = interactive
                },
                RecommendedUse = interactive
                    ? "Requires a compatible real workload provider."
                    : "Ready for a compatible workload provider.",
                UsesRealFrameTimeSource = interactive,
                IsTelemetryOnly = telemetryOnly
            };
        }
    }
}
