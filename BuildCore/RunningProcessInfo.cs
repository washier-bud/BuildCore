using System;

namespace BuildCore
{
    public class RunningProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public DateTime? StartTimeUtc { get; set; }
        public string ExecutablePath { get; set; } = "";

        public string DisplayName =>
            $"{ProcessName} (PID {ProcessId})";

        public bool HasIdentity =>
            ProcessId > 0 &&
            StartTimeUtc.HasValue &&
            !string.IsNullOrWhiteSpace(ExecutablePath);
    }
}
