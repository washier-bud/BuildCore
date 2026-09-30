using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class WorkloadBenchmarkService
    {
        private readonly IBenchmarkWorkload _workload;
        private readonly string _lockedFingerprint;

        public WorkloadBenchmarkService(IBenchmarkWorkload workload)
        {
            _workload = workload
                ?? throw new ArgumentNullException(nameof(workload));

            _lockedFingerprint = WorkloadFingerprintService.Calculate(_workload.Definition);

            if (!_workload.Definition.IsValid)
                throw new ArgumentException(
                    "The benchmark workload definition is invalid.",
                    nameof(workload));
        }

        public async Task<WorkloadBenchmarkResult> RunAsync(
            CancellationToken cancellationToken = default)
        {
            BenchmarkWorkload definition = _workload.Definition;

            var result = new WorkloadBenchmarkResult
            {
                ResultId = Guid.NewGuid().ToString("N"),
                WorkloadId = definition.WorkloadId,
                WorkloadName = definition.Name,
                WorkloadType = definition.Type,
                WorkloadFingerprint = _lockedFingerprint,
                Environment = WorkloadEnvironmentSnapshot.Capture(),
                StartedAt = DateTime.Now,
                Status = "Running",
                RequestedRuns = definition.RunCount
            };

            try
            {
                if (!IsConfigurationLocked(definition))
                {
                    result.Status = "Configuration changed";
                    result.Summary =
                        "The workload configuration changed before execution. " +
                        "The benchmark was rejected to preserve reproducibility.";
                    result.CompletedAt = DateTime.Now;
                    return result;
                }

                for (int runNumber = 1;
                     runNumber <= definition.RunCount;
                     runNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsConfigurationLocked(definition))
                    {
                        result.Status = "Configuration changed";
                        result.Summary =
                            "The workload configuration changed during the benchmark. " +
                            "The result was rejected to preserve reproducibility.";
                        break;
                    }

                    result.Status =
                        $"Running {definition.Name} " +
                        $"({runNumber}/{definition.RunCount})";

                    WorkloadRunResult runResult =
                        await _workload.RunAsync(cancellationToken);

                    if (runResult == null ||
                        !runResult.IsSuccessful ||
                        runResult.BenchmarkRun == null)
                    {
                        result.Status =
                            $"Run {runNumber} failed: " +
                            (runResult?.Summary ?? "No run result was returned.");
                        break;
                    }

                    if (!ValidateRun(runResult))
                    {
                        result.Status =
                            $"Run {runNumber} failed validation.";
                        break;
                    }

                    BenchmarkRun run = runResult.BenchmarkRun;
                    run.RunNumber = runNumber;
                    run.Environment = WorkloadEnvironmentSnapshot.Capture();

                    if (definition.RequiresInteractiveWorkload)
                    {
                        run.TargetProcessIdentity = ProcessIdentityService.Capture(
                            definition.TargetProcessId);
                    }

                    result.Runs.Add(run);

                    if (runNumber < definition.RunCount &&
                        definition.DelayBetweenRunsMilliseconds > 0)
                    {
                        result.Status = "Allowing system to settle";

                        await Task.Delay(
                            definition.DelayBetweenRunsMilliseconds,
                            cancellationToken);
                    }
                }

                result.CompletedAt = DateTime.Now;

                if (result.IsComplete)
                {
                    result.Status = "Completed";
                    result.Summary =
                        $"{definition.Name} completed with " +
                        $"{result.CompletedRuns}/{result.RequestedRuns} " +
                        "successful runs.";
                }
                else
                {
                    result.Status = "Incomplete";
                    result.Summary =
                        $"{definition.Name} did not complete all " +
                        "requested runs. " +
                        $"{result.CompletedRuns}/{result.RequestedRuns} " +
                        "successful runs.";
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                result.CompletedAt = DateTime.Now;
                result.Status = "Canceled";
                result.Summary =
                    $"{definition.Name} benchmark was canceled.";
                return result;
            }
            catch (Exception ex)
            {
                result.CompletedAt = DateTime.Now;
                result.Status = "Failed";
                result.Summary =
                    $"{definition.Name} benchmark failed: {ex.Message}";

                Debug.WriteLine("BUILDCORE WORKLOAD BENCHMARK ERROR");
                Debug.WriteLine(ex.ToString());

                return result;
            }
        }

        private bool IsConfigurationLocked(BenchmarkWorkload definition)\n        {\n            return string.Equals(\n                WorkloadFingerprintService.Calculate(definition),\n                _lockedFingerprint,\n                StringComparison.Ordinal);\n        }\n\n        private bool ValidateRun(WorkloadRunResult runResult)
        {
            BenchmarkRun? run = runResult.BenchmarkRun;

            if (run == null)
                return false;

            if (!string.Equals(
                    runResult.WorkloadId,
                    _workload.Definition.WorkloadId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(
                    runResult.WorkloadName,
                    _workload.Definition.Name,
                    StringComparison.Ordinal))
                return false;

            if (run.StartedAt == default ||
                run.CompletedAt < run.StartedAt)
                return false;

            if (run.DurationSeconds <= 0 ||
                run.SampleCount <= 0)
                return false;

            // Interactive workloads must contain actual frame-time evidence.
            // Never accept a telemetry-only run as a real frame-time run.
            if (_workload.Definition.RequiresInteractiveWorkload)
            {
                if (run.FrameTime == null ||
                    !run.FrameTime.HasData ||
                    run.FrameTime.SampleCount <= 0 ||
                    run.FrameTime.AverageFrameTimeMilliseconds <= 0)
                    return false;
            }

            return true;
        }
    }
}
