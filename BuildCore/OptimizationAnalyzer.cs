using System;
using System.Collections.Generic;

namespace BuildCore
{
    public static class OptimizationAnalyzer
    {
        public static List<OptimizationRecommendation> Analyze(
            WindowsSystemData data)
        {
            var recommendations =
                new List<OptimizationRecommendation>();

            AnalyzePowerPlan(data, recommendations);
            AnalyzeGameMode(data, recommendations);
            AnalyzeHags(data, recommendations);
            AnalyzeMemoryIntegrity(data, recommendations);
            AnalyzeGameBar(data, recommendations);

            return recommendations;
        }

        private static void AnalyzePowerPlan(
            WindowsSystemData data,
            List<OptimizationRecommendation> recommendations)
        {
            if (data.PowerPlan.Equals(
                "Balanced",
                StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Performance Power Plan",
                        Category = OptimizationCategory.Power,
                        CurrentValue = "Balanced",
                        RecommendedValue = "High Performance",

                        Description =
                            "Windows is currently using the Balanced power plan.",

                        Reason =
                            "A performance-oriented power plan can reduce " +
                            "some CPU power-management behavior during " +
                            "performance-sensitive workloads.",

                        Risk = OptimizationRisk.Low,
                        Impact = OptimizationImpact.Low,

                        CanApply = true,
                        CanTest = true,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.SafeImmediate,

                        TestDescription =
                            "BuildCore can temporarily apply the performance " +
                            "power plan, benchmark the system, and compare " +
                            "telemetry against the baseline."
                    });

                return;
            }

            if (data.PowerPlan.Equals(
                "High Performance",
                StringComparison.OrdinalIgnoreCase) ||
                data.PowerPlan.Equals(
                    "Ultimate Performance",
                    StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Performance Power Plan",
                        Category = OptimizationCategory.Power,
                        CurrentValue = data.PowerPlan,
                        RecommendedValue = "No change",

                        Description =
                            "A performance-oriented power plan is already active.",

                        Reason =
                            "BuildCore detected a performance-focused Windows " +
                            "power plan. No power-plan change is currently needed.",

                        Risk = OptimizationRisk.None,
                        Impact = OptimizationImpact.None,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.None,

                        TestDescription =
                            "No power-plan experiment is currently required."
                    });
            }
        }

        private static void AnalyzeGameMode(
            WindowsSystemData data,
            List<OptimizationRecommendation> recommendations)
        {
            if (!data.GameModeEnabled)
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Enable Windows Game Mode",
                        Category = OptimizationCategory.Gaming,
                        CurrentValue = "Disabled",
                        RecommendedValue = "Enabled",

                        Description =
                            "Windows Game Mode is currently disabled.",

                        Reason =
                            "Game Mode is designed to prioritize resources " +
                            "for games and reduce some background activity.",

                        Risk = OptimizationRisk.Low,
                        Impact = OptimizationImpact.Low,

                        CanApply = true,
                        CanTest = true,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.SafeImmediate,

                        TestDescription =
                            "BuildCore can enable Game Mode, benchmark the " +
                            "system, and compare the measured telemetry."
                    });
            }
            else
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Windows Game Mode",
                        Category = OptimizationCategory.Gaming,
                        CurrentValue = "Enabled",
                        RecommendedValue = "No change",

                        Description =
                            "Windows Game Mode is already enabled.",

                        Reason =
                            "No Game Mode change is currently recommended.",

                        Risk = OptimizationRisk.None,
                        Impact = OptimizationImpact.None,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.None
                    });
            }
        }

        private static void AnalyzeHags(
            WindowsSystemData data,
            List<OptimizationRecommendation> recommendations)
        {
            if (!data.HagsEnabled)
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title =
                            "Hardware-Accelerated GPU Scheduling",

                        Category =
                            OptimizationCategory.Graphics,

                        CurrentValue =
                            "Disabled",

                        RecommendedValue =
                            "Review / Consider Enabled",

                        Description =
                            "Hardware-Accelerated GPU Scheduling is currently disabled.",

                        Reason =
                            "HAGS changes how Windows schedules GPU work. " +
                            "Its effect varies by GPU, driver, game, and workload. " +
                            "BuildCore therefore treats it as an experimental " +
                            "optimization instead of automatically enabling it.",

                        Risk =
                            OptimizationRisk.Medium,

                        Impact =
                            OptimizationImpact.Low,

                        CanApply =
                            false,

                        CanTest =
                            true,

                        RequiresReboot =
                            true,

                        RollbackSupported =
                            true,

                        TestType =
                            OptimizationTestType.ExperimentalReboot,

                        TestDescription =
                            "HAGS requires a Windows restart before its new " +
                            "state becomes active. BuildCore will need to save " +
                            "the experiment state, restart Windows, then run " +
                            "the after-test."
                    });
            }
            else
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title =
                            "Hardware-Accelerated GPU Scheduling",

                        Category =
                            OptimizationCategory.Graphics,

                        CurrentValue =
                            "Enabled",

                        RecommendedValue =
                            "No change",

                        Description =
                            "Hardware-Accelerated GPU Scheduling is enabled.",

                        Reason =
                            "BuildCore does not automatically disable HAGS. " +
                            "Its effect depends on the workload and driver.",

                        Risk =
                            OptimizationRisk.None,

                        Impact =
                            OptimizationImpact.None,

                        CanApply =
                            false,

                        CanTest =
                            false,

                        RequiresReboot =
                            false,

                        RollbackSupported =
                            true,

                        TestType =
                            OptimizationTestType.None
                    });
            }
        }

        private static void AnalyzeMemoryIntegrity(
            WindowsSystemData data,
            List<OptimizationRecommendation> recommendations)
        {
            if (data.MemoryIntegrityEnabled)
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Memory Integrity",
                        Category = OptimizationCategory.Security,
                        CurrentValue = "Enabled",
                        RecommendedValue = "Review",

                        Description =
                            "Windows Memory Integrity is enabled.",

                        Reason =
                            "Memory Integrity is a Windows security feature. " +
                            "Disabling security features should never be treated " +
                            "as an automatic performance optimization.",

                        Risk = OptimizationRisk.High,
                        Impact = OptimizationImpact.Low,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = true,
                        RollbackSupported = false,

                        TestType =
                            OptimizationTestType.None
                    });
            }
            else
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Memory Integrity",
                        Category = OptimizationCategory.Security,
                        CurrentValue = "Disabled",
                        RecommendedValue = "No change",

                        Description =
                            "Windows Memory Integrity is disabled.",

                        Reason =
                            "BuildCore will not automatically enable or disable " +
                            "security features as part of performance optimization.",

                        Risk = OptimizationRisk.None,
                        Impact = OptimizationImpact.None,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = false,
                        RollbackSupported = false,

                        TestType =
                            OptimizationTestType.None
                    });
            }
        }

        private static void AnalyzeGameBar(
            WindowsSystemData data,
            List<OptimizationRecommendation> recommendations)
        {
            if (data.XboxGameBarEnabled)
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Xbox Game Bar",
                        Category = OptimizationCategory.Gaming,
                        CurrentValue = "Enabled",
                        RecommendedValue = "Review",

                        Description =
                            "Xbox Game Bar capture functionality is enabled.",

                        Reason =
                            "Background capture features can consume system " +
                            "resources when actively recording or capturing gameplay.",

                        Risk = OptimizationRisk.Low,
                        Impact = OptimizationImpact.Low,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.None
                    });
            }
            else
            {
                recommendations.Add(
                    new OptimizationRecommendation
                    {
                        Title = "Xbox Game Bar",
                        Category = OptimizationCategory.Gaming,
                        CurrentValue = "Disabled",
                        RecommendedValue = "No change",

                        Description =
                            "Xbox Game Bar capture functionality is disabled.",

                        Reason =
                            "No change is currently recommended.",

                        Risk = OptimizationRisk.None,
                        Impact = OptimizationImpact.None,

                        CanApply = false,
                        CanTest = false,

                        RequiresReboot = false,
                        RollbackSupported = true,

                        TestType =
                            OptimizationTestType.None
                    });
            }
        }
    }
}