using System;

namespace BuildCore
{
    public class WorkloadEnvironmentComparison
    {
        public WorkloadEnvironmentSnapshot? Before { get; set; }
        public WorkloadEnvironmentSnapshot? After { get; set; }

        public bool IsComparable { get; set; }
        public bool ProcessorConfigurationChanged { get; set; }
        public bool WindowsConfigurationChanged { get; set; }
        public bool ExpectedOptimizationStateChanged { get; set; }
        public string Summary { get; set; } = "";

        public static WorkloadEnvironmentComparison Compare(
            WorkloadEnvironmentSnapshot? before,
            WorkloadEnvironmentSnapshot? after)
        {
            var result = new WorkloadEnvironmentComparison
            {
                Before = before,
                After = after
            };

            if (before == null || after == null)
            {
                result.Summary = "Workload environment data is incomplete.";
                return result;
            }

            result.ProcessorConfigurationChanged =
                before.ProcessorCount != after.ProcessorCount ||
                before.LogicalProcessorCount != after.LogicalProcessorCount;

            result.WindowsConfigurationChanged =
                !string.Equals(before.WindowsVersion, after.WindowsVersion, StringComparison.Ordinal) ||
                !string.Equals(before.WindowsBuild, after.WindowsBuild, StringComparison.Ordinal);

            result.ExpectedOptimizationStateChanged =
                !string.Equals(before.PowerPlan, after.PowerPlan, StringComparison.OrdinalIgnoreCase) ||
                before.GameModeEnabled != after.GameModeEnabled ||
                before.HagsEnabled != after.HagsEnabled ||
                before.MemoryIntegrityEnabled != after.MemoryIntegrityEnabled;

            result.IsComparable =
                !result.ProcessorConfigurationChanged &&
                !result.WindowsConfigurationChanged;

            result.Summary = result.IsComparable
                ? result.ExpectedOptimizationStateChanged
                    ? "Core system configuration remained stable; expected optimization-related state changed."
                    : "Core system configuration remained stable between benchmark phases."
                : "Core system configuration changed between benchmark phases; workload comparison should be treated as invalid.";

            return result;
        }
    }
}