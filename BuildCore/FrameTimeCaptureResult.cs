using System;
using System.Collections.Generic;

namespace BuildCore
{
    public class FrameTimeCaptureResult
    {
        public string CaptureId { get; set; } =
            Guid.NewGuid().ToString("N");

        public int ProcessId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string Status { get; set; } = "Incomplete";

        public string Summary { get; set; } = "";

        public List<FrameTimeSample> Samples { get; set; } = new();

        public FrameTimeStatistics? Statistics { get; set; }

        public bool IsSuccessful =>
            Status.Equals(
                "Completed",
                StringComparison.OrdinalIgnoreCase)
            &&
            Samples.Count > 0
            &&
            Statistics?.HasData == true;
    }
}
