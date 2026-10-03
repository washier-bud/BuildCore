using System;
using System.Collections.Generic;

namespace BuildCore
{
    public enum BenchmarkEvidenceTrustLevel
    {
        Valid,
        Limited,
        Invalid
    }

    /// <summary>
    /// Final trust classification for a workload benchmark.
    /// This describes evidence quality only; it does not rate the optimization.
    /// </summary>
    public sealed class BenchmarkEvidenceTrust
    {
        public BenchmarkEvidenceTrustLevel Level { get; set; } =
            BenchmarkEvidenceTrustLevel.Invalid;

        public string Status { get; set; } = "INVALID";
        public string Grade { get; set; } = "F";
        public bool CanSupportMeasuredDecision { get; set; }
        public bool CanSupportAutoTuneDecision { get; set; }
        public List<string> Reasons { get; set; } = new();
        public string Summary { get; set; } = "";

        public static BenchmarkEvidenceTrust Evaluate(
            OptimizationBenchmarkResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            WorkloadEvidenceQuality quality =
                result.WorkloadEvidenceQuality ??
                WorkloadEvidenceQuality.Evaluate(result);

            var trust = new BenchmarkEvidenceTrust
            {
                Grade = quality.EvidenceGrade,
                Reasons = new List<string>(quality.Warnings)
            };

            bool completeCoreEvidence =
                quality.AnalysisComplete &&
                quality.EnvironmentComparable &&
                quality.FingerprintsMatch &&
                quality.PerRunConditionsCaptured &&
                quality.ProcessIdentityVerified &&
                quality.HasRealFrameTimeData;

            bool enoughRepeatedEvidence =
                quality.BaselineRunCount >= 3 &&
                quality.AfterRunCount >= 3 &&
                quality.PairedRunCount >= 3;

            if (quality.IsSufficient && completeCoreEvidence)
            {
                trust.Level =
                    BenchmarkEvidenceTrustLevel.Valid;
                trust.Status = "VALID";
                trust.CanSupportMeasuredDecision = true;
                trust.CanSupportAutoTuneDecision = true;
                trust.Summary =
                    "Evidence is complete enough for a measured " +
                    "optimization decision. AutoTune may use the " +
                    "result when the optimization itself is eligible.";
            }
            else if (completeCoreEvidence && enoughRepeatedEvidence)
            {
                trust.Level =
                    BenchmarkEvidenceTrustLevel.Limited;
                trust.Status = "LIMITED";
                trust.CanSupportMeasuredDecision = false;
                trust.CanSupportAutoTuneDecision = false;
                trust.Summary =
                    "The benchmark contains useful evidence, but one " +
                    "or more BuildCore trust requirements were not met. " +
                    "Do not use it as proof that an optimization improved performance.";
            }
            else
            {
                trust.Level =
                    BenchmarkEvidenceTrustLevel.Invalid;
                trust.Status = "INVALID";
                trust.CanSupportMeasuredDecision = false;
                trust.CanSupportAutoTuneDecision = false;
                trust.Summary =
                    "The benchmark does not contain enough trustworthy " +
                    "evidence for a measured optimization decision.";
            }

            if (trust.Reasons.Count == 0 &&
                trust.Level == BenchmarkEvidenceTrustLevel.Valid)
            {
                trust.Reasons.Add(
                    "All required workload evidence and integrity checks passed.");
            }

            return trust;
        }
    }
}
