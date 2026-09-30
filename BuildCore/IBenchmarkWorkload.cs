using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public interface IBenchmarkWorkload
    {
        BenchmarkWorkload Definition { get; }

        Task<WorkloadRunResult> RunAsync(
            CancellationToken cancellationToken = default);
    }
}
