using System;
using System.Management;

namespace BuildCore
{
    public class SystemInfo
    {
        public string Cpu { get; set; } = "Unknown CPU";
        public string Gpu { get; set; } = "Unknown GPU";
        public string Ram { get; set; } = "Unknown RAM";
        public string Storage { get; set; } = "Unknown Storage";
        public string Windows { get; set; } = "Unknown Windows";
        public string Motherboard { get; set; } = "Unknown Motherboard";
    }

    public static class SystemInfoService
    {
        public static SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo();

            // CPU
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.Cpu =
                        obj["Name"]?.ToString() ?? "Unknown CPU";

                    break;
                }
            }

            // GPU
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, PNPDeviceID FROM Win32_VideoController"))
            {
                string bestGpu = "Unknown GPU";
                ulong bestRam = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    string name =
                        obj["Name"]?.ToString() ?? "";

                    ulong ram = 0;

                    if (obj["AdapterRAM"] != null)
                    {
                        try
                        {
                            ram = Convert.ToUInt64(obj["AdapterRAM"]);
                        }
                        catch
                        {
                            ram = 0;
                        }
                    }

                    // Prefer NVIDIA GPUs.
                    if (name.Contains("NVIDIA",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        bestGpu = name;
                        break;
                    }

                    // Otherwise prefer the GPU with the most VRAM.
                    if (ram > bestRam)
                    {
                        bestRam = ram;
                        bestGpu = name;
                    }
                }

                info.Gpu = bestGpu;
            }

            // RAM
            using (var searcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] != null)
                    {
                        ulong bytes =
                            Convert.ToUInt64(
                                obj["TotalPhysicalMemory"]);

                        double gb =
                            bytes /
                            1024.0 /
                            1024.0 /
                            1024.0;

                        info.Ram =
                            $"{Math.Round(gb)} GB RAM";
                    }

                    break;
                }
            }

            // Storage
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Model FROM Win32_DiskDrive"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.Storage =
                        obj["Model"]?.ToString()
                        ?? "Unknown Storage";

                    break;
                }
            }

            // Windows
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Caption, Version FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption =
                        obj["Caption"]?.ToString()
                        ?? "Windows";

                    string version =
                        obj["Version"]?.ToString()
                        ?? "";

                    info.Windows =
                        $"{caption} {version}".Trim();

                    break;
                }
            }

            // Motherboard
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string manufacturer =
                        obj["Manufacturer"]?.ToString()
                        ?? "";

                    string product =
                        obj["Product"]?.ToString()
                        ?? "";

                    info.Motherboard =
                        $"{manufacturer} {product}".Trim();

                    break;
                }
            }

            return info;
        }
    }
}