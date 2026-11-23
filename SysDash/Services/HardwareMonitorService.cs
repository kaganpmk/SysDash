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

/// <summary>
/// LHM sargisi. Agir kismi (sensör agaci gezme + her sensörün okunmasi) tek seferde yapilir,
/// sonuc alanlara yazilir; tur basina ayri ayri gezinme yok.
///
/// Not: LHM 0.9.6'da sensör bazli secim (UpdateVisitor.SensorFilter) yok; tum sensörler
/// <c>hardware.Update()</c> icinde okunur. Bu yuzden maliyeti sensör sayisi belirler:
/// Storage/Network/Motherboard kapali tutulur, sadece Cpu+Gpu+Memory acilir.
/// </summary>
public class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private readonly object _gate = new();

    // Acilista bir kez cozulen referanslar; her turda yeniden gezinilmez.
    private IHardware? _cpu;
    private IHardware? _gpu;
    private IHardware? _memory;
    private bool _resolved;

    private float _cpuLoad, _cpuTemp;
    private float _gpuLoad, _gpuTemp, _vramUsed, _vramTotal;
    private float _ramUsed, _ramTotal, _ramPercent;

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
        Log.Info("LHM acildi (CPU+GPU+RAM).");
        ResolveHardware();
        _computer.Accept(_visitor);
        ReadAll();
    }

    private void ResolveHardware()
    {
        foreach (var hw in _computer.Hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu when _cpu == null:
                    _cpu = hw;
                    Log.Info($"CPU sensör kaynagi: {hw.Name}");
                    break;
                case HardwareType.GpuNvidia or HardwareType.GpuAmd when _gpu == null:
                    _gpu = hw;
                    Log.Info($"GPU sensör kaynagi: {hw.Name} ({hw.HardwareType})");
                    break;
                case HardwareType.GpuIntel when _gpu == null:
                    _gpu = hw;
                    Log.Info($"GPU sensör kaynagi (iGPU): {hw.Name}");
                    break;
                // LHM iki bellek cihazi verir: "Virtual Memory" (commit) ve "Total Memory" (fiziksel).
                // Panelde fiziksel RAM gosterilir; commit degil.
                case HardwareType.Memory when hw.Name.Contains("Total", StringComparison.OrdinalIgnoreCase):
                    _memory = hw;
                    Log.Info($"RAM sensör kaynagi: {hw.Name}");
                    break;
                case HardwareType.Memory when _memory == null:
                    _memory = hw;
                    Log.Info($"RAM sensör kaynagi (yedek): {hw.Name}");
                    break;
            }
        }
        _resolved = true;
    }

    /// <summary>Tek tur: sensörleri tazele, sonuclari alanlara yaz.</summary>
    public void Update()
    {
        lock (_gate)
        {
            _computer.Accept(_visitor);
            ReadAll();
        }
    }

    private void ReadAll()
    {
        if (!_resolved) return;
        if (_cpu != null) ReadCpu(_cpu);
        if (_gpu != null) ReadGpu(_gpu);
        if (_memory != null) ReadMemory(_memory);
    }

    private void ReadCpu(IHardware cpu)
    {
        float load = 0, temp = 0, highestCore = 0;
        bool loadSet = false, tempSet = false;

        foreach (var s in cpu.Sensors)
        {
            if (!s.Value.HasValue) continue;
            var v = s.Value.Value;

            if (s.SensorType == SensorType.Load && s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            {
                load = v; loadSet = true;
            }
            else if (s.SensorType == SensorType.Temperature && IsCpuTempSensor(s.Name))
            {
                // Package/Tctl gibi toplayici sensörler oncelikli; yoksa cekirdeklerin en yuksegi.
                if (v > temp) temp = v;
                tempSet = true;
            }
            else if (s.SensorType == SensorType.Temperature && v > highestCore)
            {
                highestCore = v;
            }
        }

        if (!tempSet) temp = highestCore;
        _cpuLoad = loadSet ? load : _cpuLoad;

        // Sensör hic yoksa (bazi laptop/VM'lerde LHM gostermez) WMI termal bolgesine dus.
        if (temp <= 0)
            temp = ThermalFallback.TryRead();

        _cpuTemp = temp;
    }

    private static bool IsCpuTempSensor(string name) =>
        name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("CCD", StringComparison.OrdinalIgnoreCase);

    private void ReadGpu(IHardware gpu)
    {
        float load = 0, temp = 0, usedMb = 0, usedMbTotal = 0;
        bool loadSet = false, tempSet = false, usedSet = false;

        foreach (var s in gpu.Sensors)
        {
            if (!s.Value.HasValue) continue;
            var v = s.Value.Value;

            switch (s.SensorType)
            {
                case SensorType.Load when !loadSet &&
                    s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    load = v; loadSet = true;
                    break;

                case SensorType.Load when !loadSet &&
                    s.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase):
                    load = v; loadSet = true;
                    break;

                case SensorType.Temperature when !tempSet:
                    temp = v; tempSet = true;
                    break;

                // VRAM: LHM 0.9.6'da SmallData birimi MB'dir. Ad yerine TIP'e gore sec:
                // "D3D Dedicated Memory Used", "GPU Memory Used" gibi adlar surum/D3D katmanina gore degisiyor.
                case SensorType.SmallData when
                    s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase):
                    if (v > usedMbTotal) usedMbTotal = v;
                    break;

                case SensorType.SmallData when
                    s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase):
                    // Birden fazla "used" sensörü varsa en buyugu (D3D Dedicated) esas alinir.
                    if (v > usedMb) usedMb = v;
                    usedSet = true;
                    break;
            }
        }

        // Toplam: once sensör (MB), yoksa WMI.
        var total = usedMbTotal > 0 ? usedMbTotal / 1024f : QueryVramTotalFromWmi();

        // Kullanilan: sensörden gelen MB degeri 1024'e bolunur.
        var used = usedMb > 0 ? usedMb / 1024f : 0f;

        // Paylasilan bellek yuzunden kullanim toplami asarsa oran bozulmasin.
        if (used > total && total > 0) total = used;

        _gpuLoad = loadSet ? load : _gpuLoad;
        _gpuTemp = tempSet ? temp : _gpuTemp;
        _vramUsed = usedSet ? used : 0;   // 0 = kullanim bilgisi yok
        _vramTotal = total;
    }

    private static float _wmiVramTotal = -1;

    private static float QueryVramTotalFromWmi()
    {
        if (_wmiVramTotal >= 0) return _wmiVramTotal;
        _wmiVramTotal = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT AdapterRAM FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var adapterRam = obj["AdapterRAM"];
                if (adapterRam == null) continue;
                var gb = Convert.ToUInt64(adapterRam) / 1024.0f / 1024.0f / 1024.0f;
                // WMI 4 GB üzerini yanlış bildirir (uint32 tavanı); makul olanı al.
                if (gb > 0.25f) { _wmiVramTotal = gb; break; }
            }
            Log.Info($"WMI VRAM toplam: {_wmiVramTotal:F1} GB");
        }
        catch (Exception ex)
        {
            Log.Warn("WMI VRAM sorgusu basarisiz.", ex);
        }
        return _wmiVramTotal;
    }

    private void ReadMemory(IHardware memory)
    {
        float usedGb = 0, availGb = 0, percent = _ramPercent;
        bool usedSet = false, availSet = false;

        foreach (var s in memory.Sensors)
        {
            if (!s.Value.HasValue) continue;
            var v = s.Value.Value;

            if (s.SensorType == SensorType.Load && s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            {
                percent = v;
            }
            else if (s.SensorType == SensorType.Data &&
                     s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            {
                // LHM 0.9.6 "Total Memory" cihazinda Data sensörleri GB birimindedir
                // (döküm: Memory Used = 15,39 / Memory Available = 0,32 -> 16 GB makine).
                if (s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                {
                    usedGb = v; usedSet = true;
                }
                else if (s.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                {
                    availGb = v; availSet = true;
                }
            }
        }

        var used = usedSet ? usedGb : 0f;
        var avail = availSet ? availGb : 0f;

        if (!usedSet || !availSet)
        {
            var (wmiUsed, wmiTotal) = QueryRamFromWmi();
            if (!usedSet) used = wmiUsed;
            if (!availSet) avail = wmiTotal > 0 ? wmiTotal - wmiUsed : 0;
        }

        _ramPercent = percent;
        _ramUsed = used;
        _ramTotal = used + avail;
    }

    private static (float usedGB, float totalGB) QueryRamFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var totalMem = Convert.ToUInt64(obj["TotalVisibleMemorySize"]) / 1024.0 / 1024.0;
                var freeMem = Convert.ToUInt64(obj["FreePhysicalMemory"]) / 1024.0 / 1024.0;
                return ((float)(totalMem - freeMem), (float)totalMem);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("WMI RAM sorgusu basarisiz.", ex);
        }
        return (0, 0);
    }

    public (float load, float temp) GetCpuMetrics() => (_cpuLoad, _cpuTemp);

    public (float load, float temp, float vramUsed, float vramTotal) GetGpuMetrics() =>
        (_gpuLoad, _gpuTemp, _vramUsed, _vramTotal);

    public (float usedGB, float totalGB) GetRamMetrics()
    {
        if (_ramTotal <= 0) return (0, 0);
        var used = _ramUsed;
        if (used <= 0 && _ramPercent > 0)
            used = _ramTotal * _ramPercent / 100f;
        return (used, _ramTotal);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try { _computer.Close(); } catch (Exception ex) { Log.Warn("LHM kapatilamadi.", ex); }
        }
        GC.SuppressFinalize(this);
        Log.Info("LHM kapatildi.");
    }
}
