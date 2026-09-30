using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class WorkloadBenchmarkService
    {
        private readonly IBenchmarkWorkload _workload;

        public WorkloadBenchmarkService(IBenchmarkWorkload workload)
        {
            _workload = workload
                ?? throw new ArgumentNullException(nameof(workload));

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
                StartedAt = DateTime.Now,
                Status = "Running",
                RequestedRuns = definition.RunCount
            };

            try
            {
                for (int runNumber = 1;
                     runNumber <= definition.RunCount;
                     runNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    result.Status =
                        $"Running {definition.Name} " +
                        $"({runNumber}/{definition.RunCount})";

                    WorkloadRunResult runResult =
                        await _workload.RunAsync(cancellationToken);

                    if (runResult == null ||
                        !runResult.IsSuccessful ||
                        runResult.BenchmarkRun == null)
                    {
                        result.Status = $"Run {runNumber} failed.";
                        break;
                    }

                    BenchmarkRun run = runResult.BenchmarkRun;
                    run.RunNumber = runNumber;
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
    }
}
