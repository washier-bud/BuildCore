using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public interface IFrameTimeSource
    {
        bool IsAvailable { get; }

        string Description { get; }

        Task<FrameTimeCaptureResult> CaptureAsync(
            int processId,
            int durationSeconds,
            CancellationToken cancellationToken = default);
    }
}
