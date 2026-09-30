using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BuildCore
{
    public class PresentMonFrameTimeSource : IFrameTimeSource
    {
        private readonly string _presentMonPath;

        public PresentMonFrameTimeSource(string? presentMonPath = null)
        {
            _presentMonPath =
                string.IsNullOrWhiteSpace(presentMonPath)
                    ? ResolvePresentMonPath()
                    : presentMonPath;
        }

        public bool IsAvailable =>
            !string.IsNullOrWhiteSpace(_presentMonPath) &&
            File.Exists(_presentMonPath);

        public string Description =>
            IsAvailable
                ? "PresentMon frame-time capture is available."
                : "PresentMon was not found. Install/provide PresentMon to enable real frame-time capture.";

        public async Task<FrameTimeCaptureResult> CaptureAsync(
            int processId,
            int durationSeconds,
            CancellationToken cancellationToken = default)
        {
            if (processId <= 0)
                throw new ArgumentOutOfRangeException(nameof(processId));

            if (durationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));

            if (!IsAvailable)
                throw new InvalidOperationException(Description);

            string outputDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "BuildCore",
                    "FrameTimeCaptures");

            Directory.CreateDirectory(outputDirectory);

            string outputFile =
                Path.Combine(
                    outputDirectory,
                    $"presentmon-{processId}-{Guid.NewGuid():N}.csv");

            var result = new FrameTimeCaptureResult
            {
                ProcessId = processId,
                StartedAt = DateTime.Now
            };

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _presentMonPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                startInfo.ArgumentList.Add("--process_id");
                startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("--output_file");
                startInfo.ArgumentList.Add(outputFile);
                startInfo.ArgumentList.Add("--timed");
                startInfo.ArgumentList.Add(durationSeconds.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("--terminate_after_timed");
                startInfo.ArgumentList.Add("--no_console_stats");
                startInfo.ArgumentList.Add("--exclude_dropped");
                startInfo.ArgumentList.Add("--v2_metrics");

                using var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                if (!process.Start())
                    throw new InvalidOperationException(
                        "PresentMon could not be started.");

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(outputFile))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(stderr)
                            ? "PresentMon completed without producing a CSV file."
                            : stderr.Trim());
                }

                result.Samples = ParseFrameTimeSamples(outputFile);

                if (result.Samples.Count == 0)
                {
                    result.Status = "Failed";
                    result.Summary =
                        "PresentMon ran, but no valid FrameTime samples were captured." +
                        (string.IsNullOrWhiteSpace(stderr)
                            ? ""
                            : $" {stderr.Trim()}");
                    return CompleteResult(result);
                }

                result.Statistics =
                    FrameTimeStatistics.Calculate(result.Samples);

                result.Status = process.ExitCode == 0
                    ? "Completed"
                    : "Failed";

                result.Summary =
                    process.ExitCode == 0
                        ? $"Captured {result.Samples.Count} real frame-time samples with PresentMon."
                        : $"PresentMon exited with code {process.ExitCode}.";

                if (process.ExitCode != 0 &&
                    !string.IsNullOrWhiteSpace(stderr))
                {
                    result.Summary += $" {stderr.Trim()}";
                }

                return CompleteResult(result);
            }
            catch (OperationCanceledException)
            {
                result.Status = "Canceled";
                result.Summary = "PresentMon frame-time capture was canceled.";
                return CompleteResult(result);
            }
            finally
            {
                TryDelete(outputFile);
            }
        }

        private static FrameTimeCaptureResult CompleteResult(
            FrameTimeCaptureResult result)
        {
            result.CompletedAt = DateTime.Now;
            return result;
        }

        private static List<FrameTimeSample> ParseFrameTimeSamples(
            string csvPath)
        {
            var samples = new List<FrameTimeSample>();

            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
                return samples;

            string[] headers = SplitCsvLine(lines[0]);

            int frameTimeIndex = FindHeaderIndex(
                headers,
                "FrameTime");

            int qpcTimeMsIndex = FindHeaderIndex(
                headers,
                "QPCTime");

            if (frameTimeIndex < 0)
                return samples;

            DateTime baseTimestamp = DateTime.Now;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] columns = SplitCsvLine(lines[i]);

                if (frameTimeIndex >= columns.Length)
                    continue;

                if (!double.TryParse(
                    columns[frameTimeIndex],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double frameTime))
                {
                    continue;
                }

                if (frameTime <= 0 ||
                    double.IsNaN(frameTime) ||
                    double.IsInfinity(frameTime))
                {
                    continue;
                }

                DateTime timestamp = baseTimestamp;

                if (qpcTimeMsIndex >= 0 &&
                    qpcTimeMsIndex < columns.Length &&
                    double.TryParse(
                        columns[qpcTimeMsIndex],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double qpcMilliseconds))
                {
                    timestamp = baseTimestamp.AddMilliseconds(qpcMilliseconds);
                }

                samples.Add(new FrameTimeSample
                {
                    Timestamp = timestamp,
                    FrameTimeMilliseconds = frameTime
                });
            }

            return samples;
        }

        private static int FindHeaderIndex(
            string[] headers,
            string name)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(
                    headers[i].Trim().Trim('"'),
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            foreach (char character in line)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (character == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        private static string? ResolvePresentMonPath()
        {
            string localPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "PresentMon.exe");

            if (File.Exists(localPath))
                return localPath;

            string programFilesPath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFiles),
                    "Intel",
                    "PresentMon",
                    "PresentMon.exe");

            if (File.Exists(programFilesPath))
                return programFilesPath;

            string pathEnvironment =
                Environment.GetEnvironmentVariable("PATH") ?? "";

            foreach (string directory in
                pathEnvironment.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate =
                        Path.Combine(
                            directory.Trim(),
                            "PresentMon.exe");

                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }

            return null;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Capture cleanup failure must not hide the benchmark result.
            }
        }
    }
}
