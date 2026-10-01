using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildCore
{
    public class WorkloadEvidenceQuality
    {
        public bool HasRealFrameTimeData { get; set; }
        public bool ConfigurationLocked { get; set; }
        public bool ProcessIdentityVerified { get; set; }
        public bool PerRunConditionsCaptured { get; set; }
        public bool EnvironmentComparable { get; set; }
        public bool FingerprintsMatch { get; set; }
        public bool AnalysisComplete { get; set; }

        public int BaselineRunCount { get; set; }
        public int AfterRunCount { get; set; }
        public int PairedRunCount { get; set; }

        public string ConsistencyStatus { get; set; } = "Unknown";
        public string ReliabilityStatus { get; set; } = "Insufficient Evidence";
        public string EvidenceGrade { get; set; } = "F";
        public List<string> Warnings { get; set; } = new();
        public string Summary { get; set; } = "";

        public bool IsSufficient =>
            AnalysisComplete &&
            EnvironmentComparable &&
            FingerprintsMatch &&
            PerRunConditionsCaptured &&
            BaselineRunCount >= 3 &&
            AfterRunCount >= 3 &&
            PairedRunCount >= 3 &&
            HasRequiredPerformanceEvidence &&
            ProcessIdentityVerified;

        public bool HasRequiredPerformanceEvidence =>
            HasRealFrameTimeData;

        public static WorkloadEvidenceQuality Evaluate(
            OptimizationBenchmarkResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var quality = new WorkloadEvidenceQuality();

            BenchmarkWorkload? definition = result.WorkloadDefinition;
            WorkloadBenchmarkResult? baseline = result.WorkloadBaseline;
            WorkloadBenchmarkResult? after = result.WorkloadAfter;
            WorkloadStatisticalAnalysis? analysis = result.WorkloadAnalysis;

            quality.BaselineRunCount = baseline?.CompletedRuns ?? 0;
            quality.AfterRunCount = after?.CompletedRuns ?? 0;
            quality.PairedRunCount = analysis?.PairedRunCount ?? 0;

            quality.FingerprintsMatch =
                !string.IsNullOrWhiteSpace(result.WorkloadFingerprint) &&
                baseline != null &&
                after != null &&
                string.Equals(
                    result.WorkloadFingerprint,
                    baseline.WorkloadFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    result.WorkloadFingerprint,
                    after.WorkloadFingerprint,
                    StringComparison.Ordinal);

            quality.ConfigurationLocked = quality.FingerprintsMatch;

            quality.EnvironmentComparable =
                result.WorkloadEnvironmentComparison?.IsComparable ?? false;

            quality.PerRunConditionsCaptured =
                baseline != null && after != null &&
                baseline.Runs.Count > 0 && after.Runs.Count > 0 &&
                baseline.Runs.All(r => r.Environment != null) &&
                after.Runs.All(r => r.Environment != null);

            quality.AnalysisComplete =
                result.WorkloadAnalysisCompleted &&
                analysis != null &&
                analysis.IsComparable;

            quality.HasRealFrameTimeData =
                baseline?.Runs.Any(r =>
                    r.FrameTime != null && r.FrameTime.HasData) == true &&
                after?.Runs.Any(r =>
                    r.FrameTime != null && r.FrameTime.HasData) == true;

            if (definition?.RequiresInteractiveWorkload == true)
            {
                quality.ProcessIdentityVerified =
                    baseline?.Runs.All(HasValidProcessIdentity) == true &&
                    after?.Runs.All(HasValidProcessIdentity) == true;
            }
            else
            {
                quality.ProcessIdentityVerified = true;
            }

            if (quality.HasRealFrameTimeData)
            {
                string baselineStatus =
                    baseline?.Consistency?.Status ?? "Unknown";
                string afterStatus =
                    after?.Consistency?.Status ?? "Unknown";

                quality.ConsistencyStatus =
                    $"Baseline: {baselineStatus}; After: {afterStatus}";
            }
            else
            {
                quality.ConsistencyStatus = "Telemetry Only";
            }

            if (definition?.RequiresInteractiveWorkload == true &&
                !quality.HasRealFrameTimeData)
            {
                quality.Warnings.Add(
                    "No real frame-time evidence was captured.");
            }

            if (!quality.PerRunConditionsCaptured)
            {
                quality.Warnings.Add(
                    "Per-run environment capture is incomplete.");
            }

            if (!quality.EnvironmentComparable)
            {
                quality.Warnings.Add(
                    "The benchmark environment was not fully comparable.");
            }

            if (!quality.FingerprintsMatch)
            {
                quality.Warnings.Add(
                    "Workload configuration fingerprints do not match.");
            }

            if (definition?.RequiresInteractiveWorkload == true &&
                !quality.ProcessIdentityVerified)
            {
                quality.Warnings.Add(
                    "Target process identity was not verified for every captured run.");
            }

            if (quality.BaselineRunCount < 3 ||
                quality.AfterRunCount < 3)
            {
                quality.Warnings.Add(
                    "Fewer than three successful runs are available in one or both phases.");
            }

            if (quality.PairedRunCount < 3)
            {
                quality.Warnings.Add(
                    "Fewer than three paired runs are available for paired analysis.");
            }

            if (analysis != null &&
                analysis.PairedRunAnalysisUsed &&
                analysis.PairedDifferenceStandardDeviationPercent > 5)
            {
                quality.Warnings.Add(
                    "Paired-run performance changes show substantial variation.");
            }

            if (definition?.RequiresInteractiveWorkload == true &&
                !quality.HasRealFrameTimeData)
            {
                quality.Warnings.Add(
                    "Interactive workload evidence requires real frame-time data.");
            }

            quality.ReliabilityStatus = quality.IsSufficient
                ? "Sufficient Evidence"
                : "Limited Evidence";

            // Phase 1.12W: expose evidence quality as a transparent grade.
            // This is a data-quality grade, not a rating of the optimization itself.
            quality.EvidenceGrade = CalculateEvidenceGrade(quality, analysis);

            quality.Summary = quality.IsSufficient
                ? "The test contains locked workload configuration, comparable environment data, "
                  + "sufficient repeated runs, and completed paired analysis."
                : "The test completed with limitations. Review the evidence warnings before "
                  + "treating the result as a strong workload-performance signal.";

            return quality;
        }


        private static string CalculateEvidenceGrade(
            WorkloadEvidenceQuality quality,
            WorkloadStatisticalAnalysis? analysis)
        {
            int points = 0;

            if (quality.HasRealFrameTimeData) points += 25;
            if (quality.FingerprintsMatch) points += 15;
            if (quality.EnvironmentComparable) points += 10;
            if (quality.PerRunConditionsCaptured) points += 5;
            if (quality.ProcessIdentityVerified) points += 10;
            if (quality.BaselineRunCount >= 3) points += 10;
            if (quality.AfterRunCount >= 3) points += 10;
            if (quality.PairedRunCount >= 3) points += 10;
            if (quality.AnalysisComplete) points += 5;

            if (analysis?.PairedDifferenceStandardDeviationPercent > 5)
                points -= 10;

            return points >= 90 ? "A" :
                   points >= 80 ? "B" :
                   points >= 70 ? "C" :
                   points >= 60 ? "D" : "F";
        }

        private static bool HasValidProcessIdentity(BenchmarkRun run)
        {
            return run.TargetProcessIdentity != null &&
                   run.TargetProcessIdentity.HasIdentity;
        }
    }
}
