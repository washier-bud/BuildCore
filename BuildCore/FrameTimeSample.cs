using System;

namespace BuildCore
{
    public class FrameTimeSample
    {
        public DateTime Timestamp { get; set; }

        public double FrameTimeMilliseconds { get; set; }

        public bool IsValid =>
            FrameTimeMilliseconds > 0 &&
            !double.IsNaN(FrameTimeMilliseconds) &&
            !double.IsInfinity(FrameTimeMilliseconds);
    }
}
