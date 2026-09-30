using System;

namespace BuildCore
{
    public class BenchmarkWorkloadProfile
    {
        public BenchmarkWorkload Definition { get; set; } = new();

        public string RecommendedUse { get; set; } = "";

        public bool UsesRealFrameTimeSource { get; set; }

        public bool IsTelemetryOnly { get; set; }

        public BenchmarkWorkloadProfile Clone()
        {
            return new BenchmarkWorkloadProfile
            {
                Definition = new BenchmarkWorkload
                {
                    WorkloadId = Guid.NewGuid().ToString("N"),
                    Name = Definition.Name,
                    Type = Definition.Type,
                    Description = Definition.Description,
                    DurationSeconds = Definition.DurationSeconds,
                    RunCount = Definition.RunCount,
                    SampleIntervalMilliseconds =
                        Definition.SampleIntervalMilliseconds,
                    DelayBetweenRunsMilliseconds =
                        Definition.DelayBetweenRunsMilliseconds,
                    RequiresInteractiveWorkload =
                        Definition.RequiresInteractiveWorkload,
                    TargetProcessId = Definition.TargetProcessId
                },
                RecommendedUse = RecommendedUse,
                UsesRealFrameTimeSource = UsesRealFrameTimeSource,
                IsTelemetryOnly = IsTelemetryOnly
            };
        }
    }
}
