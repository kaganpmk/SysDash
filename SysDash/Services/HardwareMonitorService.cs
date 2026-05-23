using System.Management;
using LibreHardwareMonitor.Hardware;

namespace SysDash.Services;

internal class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

public class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsPsuEnabled = false
        };
        _computer.Open();
        _computer.Accept(_visitor);
    }

    public void Update() => _computer.Accept(_visitor);

    public (float load, float temp) GetCpuMetrics()
    {
        float load = 0, temp = 0;
        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu) continue;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                {
                    if (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        load = sensor.Value.Value;
                }
                else if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                {
                    if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || sensor.Index == 0)
                        temp = sensor.Value.Value;
                }
            }

            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
                foreach (var sensor in sub.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue && sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        load = sensor.Value.Value;
                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || sensor.Index == 0))
                        temp = sensor.Value.Value;
                }
            }
        }
        return (load, temp);
    }

    public (float load, float temp, float vramUsed, float vramTotal) GetGpuMetrics()
    {
        float load = 0, temp = 0, vramUsed = 0, vramTotal = 0;
        bool hasDedicatedGpu = false;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.GpuNvidia &&
                hardware.HardwareType != HardwareType.GpuAmd &&
                hardware.HardwareType != HardwareType.GpuIntel)
                continue;

            if (!hasDedicatedGpu)
                hasDedicatedGpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd;
            else if (hardware.HardwareType == HardwareType.GpuIntel)
                continue;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                    load = sensor.Value.Value;
                if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue && sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) && load == 0)
                    load = sensor.Value.Value;
                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && temp == 0)
                    temp = sensor.Value.Value;
                if (sensor.SensorType == SensorType.SmallData && sensor.Value.HasValue && vramUsed == 0)
                    vramUsed = sensor.Value.Value / 1024f / 1024f / 1024f;
            }
        }

        if (vramUsed == 0 && vramTotal == 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var adapterRam = obj["AdapterRAM"];
                    if (adapterRam != null)
                    {
                        vramTotal = Convert.ToUInt64(adapterRam) / 1024.0f / 1024.0f / 1024.0f;
                        break;
                    }
                }
            }
            catch { }
        }

        return (load, temp, vramUsed, vramTotal);
    }

    public (float usedGB, float totalGB) GetRamMetrics()
    {
        float usedGB = 0, totalGB = 0;
        float percent = 0;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Memory) continue;
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    percent = sensor.Value.Value;
                if (sensor.SensorType == SensorType.Data && sensor.Value.HasValue)
                {
                    var valGb = sensor.Value.Value / 1024f / 1024f / 1024f;
                    if (sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                        usedGB = valGb;
                    else if (sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                        totalGB -= valGb;
                }
            }
        }

        if (totalGB <= 0 || usedGB <= 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    var totalMem = Convert.ToUInt64(obj["TotalVisibleMemorySize"]) / 1024.0 / 1024.0;
                    var freeMem = Convert.ToUInt64(obj["FreePhysicalMemory"]) / 1024.0 / 1024.0;
                    usedGB = (float)(totalMem - freeMem);
                    totalGB = (float)totalMem;
                }
            }
            catch { }
        }

        if (percent > 0 && totalGB > 0 && usedGB <= 0)
            usedGB = totalGB * percent / 100f;

        return (usedGB, totalGB);
    }

    public void Dispose()
    {
        _computer.Close();
        GC.SuppressFinalize(this);
    }
}
