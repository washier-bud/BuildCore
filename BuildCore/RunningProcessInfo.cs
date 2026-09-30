namespace BuildCore
{
    public class RunningProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string DisplayName => $"{ProcessName} (PID {ProcessId})";
    }
}
