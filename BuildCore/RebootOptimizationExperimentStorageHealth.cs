namespace BuildCore
{
    public sealed class RebootOptimizationExperimentStorageHealth
    {
        public string ExperimentId { get; set; } = string.Empty;
        public bool IsValidId { get; set; }
        public bool PrimaryExists { get; set; }
        public bool PrimaryValid { get; set; }
        public bool BackupExists { get; set; }
        public bool BackupValid { get; set; }
        public bool IsHealthy { get; set; }
        public bool CanRecoverFromBackup { get; set; }
    }
}
