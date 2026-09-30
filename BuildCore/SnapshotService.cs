using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BuildCore
{
    public class BuildCoreSnapshot
    {
        public string Id { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string SnapshotVersion { get; set; } = "1.0";

        public SystemInfo SystemInfo { get; set; } =
            new SystemInfo();

        public WindowsSystemData WindowsSystem { get; set; } =
            new WindowsSystemData();

        public List<OptimizationRecommendation>
            Recommendations
        { get; set; } =
            new List<OptimizationRecommendation>();
    }

    public class SnapshotSummary
    {
        public string Id { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string DisplayName
        {
            get
            {
                return
                    $"Snapshot {CreatedAt:yyyy-MM-dd HH:mm:ss}";
            }
        }
    }

    public static class SnapshotService
    {
        private static readonly string SnapshotDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BuildCore",
                "Snapshots");

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true
            };

        public static BuildCoreSnapshot CreateSnapshot()
        {
            Directory.CreateDirectory(
                SnapshotDirectory);

            SystemInfo systemInfo =
                SystemInfoService.GetSystemInfo();

            WindowsSystemData windowsSystem =
                WindowsSystemService.Scan();

            List<OptimizationRecommendation>
                recommendations =
                    OptimizationAnalyzer.Analyze(
                        windowsSystem);

            var snapshot =
                new BuildCoreSnapshot
                {
                    Id =
                        Guid.NewGuid()
                            .ToString("N"),

                    CreatedAt =
                        DateTime.Now,

                    SnapshotVersion =
                        "1.0",

                    SystemInfo =
                        systemInfo,

                    WindowsSystem =
                        windowsSystem,

                    Recommendations =
                        recommendations
                };

            SaveSnapshot(snapshot);

            return snapshot;
        }

        public static void SaveSnapshot(
            BuildCoreSnapshot snapshot)
        {
            Directory.CreateDirectory(
                SnapshotDirectory);

            string filePath =
                GetSnapshotPath(snapshot.Id);

            string json =
                JsonSerializer.Serialize(
                    snapshot,
                    JsonOptions);

            File.WriteAllText(
                filePath,
                json);
        }

        public static BuildCoreSnapshot?
            LoadSnapshot(string id)
        {
            try
            {
                string filePath =
                    GetSnapshotPath(id);

                if (!File.Exists(filePath))
                    return null;

                string json =
                    File.ReadAllText(filePath);

                return JsonSerializer.Deserialize
                    <BuildCoreSnapshot>(
                        json,
                        JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static List<SnapshotSummary>
            GetSnapshots()
        {
            var snapshots =
                new List<SnapshotSummary>();

            if (!Directory.Exists(
                SnapshotDirectory))
            {
                return snapshots;
            }

            foreach (string file in
                Directory.GetFiles(
                    SnapshotDirectory,
                    "*.json"))
            {
                try
                {
                    string json =
                        File.ReadAllText(file);

                    BuildCoreSnapshot?
                        snapshot =
                            JsonSerializer.Deserialize
                            <BuildCoreSnapshot>(
                                json,
                                JsonOptions);

                    if (snapshot == null)
                        continue;

                    snapshots.Add(
                        new SnapshotSummary
                        {
                            Id = snapshot.Id,
                            CreatedAt =
                                snapshot.CreatedAt
                        });
                }
                catch
                {
                    // Ignore invalid snapshot files.
                }
            }

            return snapshots
                .OrderByDescending(
                    snapshot =>
                        snapshot.CreatedAt)
                .ToList();
        }

        public static bool DeleteSnapshot(
            string id)
        {
            try
            {
                string filePath =
                    GetSnapshotPath(id);

                if (!File.Exists(filePath))
                    return false;

                File.Delete(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateSnapshot(
            string id)
        {
            try
            {
                BuildCoreSnapshot?
                    snapshot =
                        LoadSnapshot(id);

                if (snapshot == null)
                    return false;

                if (string.IsNullOrWhiteSpace(
                    snapshot.Id))
                {
                    return false;
                }

                if (snapshot.CreatedAt ==
                    default)
                {
                    return false;
                }

                if (snapshot.SystemInfo == null)
                    return false;

                if (snapshot.WindowsSystem == null)
                    return false;

                if (snapshot.Recommendations == null)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string
            GetSnapshotDirectory()
        {
            Directory.CreateDirectory(
                SnapshotDirectory);

            return SnapshotDirectory;
        }

        private static string
            GetSnapshotPath(string id)
        {
            // Prevent path traversal and ensure
            // only our generated IDs are accepted.
            if (string.IsNullOrWhiteSpace(id) ||
                id.Any(
                    character =>
                        !char.IsLetterOrDigit(
                            character)))
            {
                throw new ArgumentException(
                    "Invalid snapshot ID.",
                    nameof(id));
            }

            return Path.Combine(
                SnapshotDirectory,
                $"{id}.json");
        }
    }
}