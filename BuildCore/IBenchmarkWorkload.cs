using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public interface IBenchmarkWorkload
    {
        BenchmarkWorkload Definition { get; }

        Task<WorkloadBenchmarkResult> RunAsync(
            CancellationToken cancellationToken = default);
    }
}
