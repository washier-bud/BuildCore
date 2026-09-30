using System;

namespace BuildCore
{
    public class OptimizationTransaction
    {
        public string TransactionId { get; set; } = "";

        public string SnapshotId { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string OptimizationTitle { get; set; } = "";

        public OptimizationCategory Category { get; set; }

        public string BeforeValue { get; set; } = "";

        public string TargetValue { get; set; } = "";

        public bool ApplySucceeded { get; set; }

        public bool VerificationSucceeded { get; set; }

        public string ApplyMessage { get; set; } = "";

        public string? Error { get; set; }

        public bool RestoreAvailable { get; set; }

        public bool RestoreSucceeded { get; set; }

        public bool RestoreVerificationSucceeded { get; set; }

        public string RestoreMessage { get; set; } = "";

        public string? RestoreError { get; set; }

        public DateTime? RestoredAt { get; set; }

        public bool IsSuccessful
        {
            get
            {
                return
                    ApplySucceeded &&
                    VerificationSucceeded;
            }
        }

        public bool IsRestored
        {
            get
            {
                return
                    RestoreSucceeded &&
                    RestoreVerificationSucceeded;
            }
        }
    }
}