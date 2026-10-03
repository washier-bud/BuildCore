using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Management;

namespace BuildCore
{
    public class HardwareMonitorData
    {
        public float? CpuTemperature { get; set; }
        public string CpuTemperatureSource { get; set; } = "Unavailable";
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
                IsStorageEnabled = false
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
            try
            {
                using var searcher =
                    new ManagementObjectSearcher(
                        @"root\WMI",
                        "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CurrentTemperature"] == null)
                        continue;

                    double rawTemperature =
                        Convert.ToDouble(
                            obj["CurrentTemperature"]);

                    double celsius =
                        (rawTemperature / 10.0) - 273.15;

                    if (celsius >= 0 &&
                        celsius <= 120)
                    {
                        data.CpuTemperature =
                            (float)celsius;

                        return;
                    }
                }
            }
            catch
            {
                data.CpuTemperature = null;
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

            if (sensor.SensorType ==
                SensorType.SmallData)
            {
                if (name.Equals(
                    "GPU Memory Used",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryUsed =
                        value;
                }

                if (name.Equals(
                    "GPU Memory Total",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GpuMemoryTotal =
                        value;
                }
            }

            // -----------------------------
            // FALLBACK VRAM SENSOR NAMES
            // -----------------------------

            if (sensor.SensorType ==
                SensorType.SmallData)
            {
                if (lowerName.Contains(
                    "dedicated memory used"))
                {
                    // D3D dedicated memory is useful
                    // as a fallback, but don't overwrite
                    // the direct NVIDIA VRAM sensor.
                    if (!data.GpuMemoryUsed.HasValue)
                    {
                        data.GpuMemoryUsed =
                            value;
                    }
                }
            }
        }
    }
}