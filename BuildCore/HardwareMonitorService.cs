using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using System;
using System.Diagnostics;
using System.Management;

namespace BuildCore
{
    public class HardwareMonitorData
    {
        public float? CpuTemperature { get; set; }
        public string CpuTemperatureSource { get; set; } = "Unavailable";
        public bool CpuLowLevelAccessAvailable { get; set; }
        public string CpuSensorStatus { get; set; } = "Unknown";
        public float? CpuClock { get; set; }

        public float? GpuTemperature { get; set; }
        public float? GpuClock { get; set; }
        public float? GpuMemoryClock { get; set; }
        public float? GpuUsage { get; set; }

        public float? GpuMemoryUsed { get; set; }
        public float? GpuMemoryTotal { get; set; }
        public float? GpuMemoryTemperature { get; set; }

        public float? GpuPower { get; set; }
        public float? GpuFanSpeed { get; set; }
    }

    public class HardwareMonitorService
    {
        private readonly Computer _computer;
        private readonly PerformanceCounter _cpuClockCounter;

        public HardwareMonitorService()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false,
                IsStorageEnabled = false,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };

            _computer.Open();

            _cpuClockCounter = new PerformanceCounter(
                "Processor Information",
                "% Processor Performance",
                "_Total");

            _cpuClockCounter.NextValue();
        }

        public HardwareMonitorData GetHardwareData()
        {
            var data = new HardwareMonitorData();

            data.CpuLowLevelAccessAvailable = PawnIo.IsInstalled;
            data.CpuSensorStatus = PawnIo.IsInstalled
                ? "Low-level sensor access available"
                : "PawnIO driver not installed";

            ReadCpuClock(data);
            ReadCpuTemperature(data);
            ReadGpuSensors(data);

            return data;
        }

        private void ReadCpuClock(
            HardwareMonitorData data)
        {
            try
            {
                float performance =
                    _cpuClockCounter.NextValue();

                if (performance > 0)
                {
                    double estimatedGHz =
                        4.7 * (performance / 100.0);

                    if (estimatedGHz > 0)
                    {
                        data.CpuClock =
                            (float)(estimatedGHz * 1000.0);
                    }
                }
            }
            catch
            {
                data.CpuClock = null;
            }
        }

        private void ReadCpuTemperature(
            HardwareMonitorData data)
        {
            // Primary source: LibreHardwareMonitor CPU sensors.
            // On some Ryzen systems the CPU temperature is exposed
            // through the motherboard/embedded-controller path instead.
            try
            {
                float? best = null;
                string source = "Unavailable";
                int priority = int.MaxValue;

                foreach (IHardware hardware in _computer.Hardware)
                {
                    hardware.Update();

                    FindCpuTemperature(
                        hardware,
                        ref best,
                        ref source,
                        ref priority);
                }

                if (best.HasValue)
                {
                    data.CpuTemperature = best.Value;
                    data.CpuTemperatureSource = source;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"CPU hardware temperature error: {ex}");
            }

            // Secondary source: Windows ACPI thermal zone.
            try
            {
                using var searcher =
                    new ManagementObjectSearcher(
                        @"root\\WMI",
                        "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CurrentTemperature"] == null)
                        continue;

                    double raw =
                        Convert.ToDouble(obj["CurrentTemperature"]);

                    double celsius =
                        (raw / 10.0) - 273.15;

                    if (celsius > 0 && celsius <= 120)
                    {
                        data.CpuTemperature = (float)celsius;
                        data.CpuTemperatureSource = "Windows ACPI";
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"CPU ACPI temperature fallback error: {ex}");
            }

            data.CpuTemperature = null;
            data.CpuTemperatureSource = "Unavailable";

            if (!data.CpuLowLevelAccessAvailable)
            {
                data.CpuSensorStatus =
                    "PawnIO driver required for CPU temperature";
            }
            else
            {
                data.CpuSensorStatus =
                    "CPU temperature sensor unavailable";
            }
        }

        private static void FindCpuTemperature(
            IHardware hardware,
            ref float? best,
            ref string source,
            ref int priority)
        {
            bool relevantHardware =
                hardware.HardwareType == HardwareType.Cpu ||
                hardware.HardwareType == HardwareType.Motherboard ||
                hardware.HardwareType == HardwareType.SuperIO ||
                hardware.HardwareType == HardwareType.EmbeddedController;

            if (relevantHardware)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature ||
                        !sensor.Value.HasValue)
                        continue;

                    float value = sensor.Value.Value;

                    // Zero/null readings are not valid CPU temperatures.
                    if (value <= 0 || value > 120)
                        continue;

                    string name = sensor.Name.Trim();
                    string lower = name.ToLowerInvariant();

                    int candidatePriority;

                    if (lower.Contains("tctl/tdie"))
                    {
                        candidatePriority = 0;
                    }
                    else if (lower.Contains("cpu package"))
                    {
                        candidatePriority = 1;
                    }
                    else if (lower.Contains("package") &&
                             relevantHardware)
                    {
                        candidatePriority = 2;
                    }
                    else if (lower.Contains("tdie"))
                    {
                        candidatePriority = 3;
                    }
                    else if (lower.Contains("tctl"))
                    {
                        candidatePriority = 4;
                    }
                    else if (lower == "cpu" ||
                             lower.Contains("cpu temperature") ||
                             lower.Contains("cpu temp"))
                    {
                        candidatePriority = 5;
                    }
                    else if (lower.Contains("core max"))
                    {
                        candidatePriority = 6;
                    }
                    else if (lower.Contains("core") &&
                             hardware.HardwareType == HardwareType.Cpu)
                    {
                        candidatePriority = 7;
                    }
                    else
                    {
                        continue;
                    }

                    // Prefer the actual CPU hardware sensor over
                    // motherboard/EC duplicates when priorities tie.
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        candidatePriority -= 1;
                    }

                    if (candidatePriority < priority)
                    {
                        priority = candidatePriority;
                        best = value;
                        source =
                            $"LibreHardwareMonitor • {hardware.Name} • {name}";
                    }
                }
            }

            foreach (IHardware child in hardware.SubHardware)
            {
                child.Update();

                FindCpuTemperature(
                    child,
                    ref best,
                    ref source,
                    ref priority);
            }
        }

        private void ReadGpuSensors(
            HardwareMonitorData data)
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType ==
                    HardwareType.GpuNvidia)
                {
                    ReadGpuHardware(
                        hardware,
                        data);
                }
            }
        }

        private void ReadGpuHardware(
            IHardware hardware,
            HardwareMonitorData data)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                ReadGpuSensor(
                    sensor,
                    data);
            }

            foreach (IHardware subHardware
                in hardware.SubHardware)
            {
                subHardware.Update();

                foreach (ISensor sensor
                    in subHardware.Sensors)
                {
                    ReadGpuSensor(
                        sensor,
                        data);
                }
            }
        }

        private void ReadGpuSensor(
            ISensor sensor,
            HardwareMonitorData data)
        {
            if (!sensor.Value.HasValue)
                return;

            float value =
                sensor.Value.Value;

            string name =
                sensor.Name.Trim();

            string lowerName =
                name.ToLowerInvariant();

            // -----------------------------
            // GPU TEMPERATURE
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.Temperature)
            {
                if (name.Equals(
                    "GPU Core",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuTemperature =
                        value;
                }

                if (name.Equals(
                    "GPU Memory Junction",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryTemperature =
                        value;
                }
            }

            // -----------------------------
            // GPU CORE CLOCK
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.Clock)
            {
                if (name.Equals(
                    "GPU Core",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuClock =
                        value;
                }

                if (name.Equals(
                    "GPU Memory",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryClock =
                        value;
                }
            }

            // -----------------------------
            // GPU FAN
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.Fan)
            {
                if (name.Equals(
                    "GPU Fan",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuFanSpeed =
                        value;
                }
            }

            // -----------------------------
            // GPU USAGE
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.Load)
            {
                if (name.Equals(
                    "GPU Core",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuUsage =
                        Math.Clamp(
                            value,
                            0,
                            100);
                }
            }

            // -----------------------------
            // GPU POWER
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.Power)
            {
                if (name.Equals(
                    "GPU Package",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuPower =
                        value;
                }
            }

            // -----------------------------
            // VRAM
            // -----------------------------

            // -----------------------------
            // VRAM
            // -----------------------------
            // LibreHardwareMonitor versions expose
            // memory sensors differently. Avoid relying
            // on the removed SmallData enum and use
            // sensor names/type combinations that are
            // actually present in the current library.
            if (sensor.SensorType == SensorType.Data)
            {
                if (name.Equals(
                    "GPU Memory Used",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryUsed = value;
                }

                if (name.Equals(
                    "GPU Memory Total",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryTotal = value;
                }

                if (lowerName.Contains("dedicated memory used") &&
                    !data.GpuMemoryUsed.HasValue)
                {
                    data.GpuMemoryUsed = value;
                }
            }
        }
    }
}