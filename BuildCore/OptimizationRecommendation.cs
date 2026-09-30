namespace BuildCore
{
    public enum OptimizationCategory
    {
        Power,
        Gaming,
        Graphics,
        Security,
        Windows,
        Network,
        Background
    }

    public enum OptimizationRisk
    {
        None,
        Low,
        Medium,
        High
    }

    public enum OptimizationImpact
    {
        None,
        Low,
        Medium,
        High
    }

    public enum OptimizationTestType
    {
        None,
        SafeImmediate,
        ExperimentalReboot
    }

    public class OptimizationRecommendation
    {
        public string Title { get; set; } =
            "Unknown Recommendation";

        public OptimizationCategory Category { get; set; }

        public string CurrentValue { get; set; } =
            "Unknown";

        public string RecommendedValue { get; set; } =
            "Unknown";

        public string Description { get; set; } =
            "";

        public string Reason { get; set; } =
            "";

        public OptimizationRisk Risk { get; set; } =
            OptimizationRisk.Low;

        public OptimizationImpact Impact { get; set; } =
            OptimizationImpact.Low;

        public bool CanApply { get; set; } =
            false;

        public bool CanTest { get; set; } =
            false;

        public bool RequiresReboot { get; set; } =
            false;

        public bool RollbackSupported { get; set; } =
            false;

        public OptimizationTestType TestType { get; set; } =
            OptimizationTestType.None;

        public string TestDescription { get; set; } =
            "";

        public string ActionStatus
        {
            get
            {
                if (CanApply && CanTest)
                    return "READY TO TEST / APPLY";

                if (CanTest)
                {
                    return RequiresReboot
                        ? "READY TO TEST • REBOOT REQUIRED"
                        : "READY TO TEST";
                }

                if (CanApply)
                    return "READY TO APPLY";

                if (RecommendedValue.Equals(
                    "No change",
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return "ALREADY OPTIMIZED";
                }

                return "REVIEW ONLY";
            }
        }
    }
}