using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SysDash.Services;

namespace SysDash.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly HardwareMonitorService _hardwareMonitor;
    private readonly NetworkMonitorService _networkMonitor;
    private readonly UptimeService _uptimeService;
    private readonly AutoStartService _autoStartService;
    private readonly DisplayService _displayService;
    private readonly DispatcherTimer _timer;
    private const int MaxHistory = 30;

    private readonly List<float> _cpuHistory = new();
    private readonly List<float> _gpuHistory = new();
    private readonly List<float> _networkHistory = new();
    private IReadOnlyList<float> _cpuSparklineData = Array.Empty<float>();
    private IReadOnlyList<float> _gpuSparklineData = Array.Empty<float>();
    private IReadOnlyList<float> _networkSparklineData = Array.Empty<float>();

    public DashboardViewModel(HardwareMonitorService hardwareMonitor, NetworkMonitorService networkMonitor,
        UptimeService uptimeService, AutoStartService autoStartService,
        DisplayService displayService, ConfigService configService)
    {
        _hardwareMonitor = hardwareMonitor;
        _networkMonitor = networkMonitor;
        _uptimeService = uptimeService;
        _autoStartService = autoStartService;
        _displayService = displayService;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        UpdateDateTime();
        UpdateMetrics();
    }

    [ObservableProperty]
    private string _currentTime = "";

    [ObservableProperty]
    private string _currentDate = "";

    [ObservableProperty]
    private string _currentDay = "";

    [ObservableProperty]
    private string _cpuUsageText = "--";

    [ObservableProperty]
    private string _cpuTemperatureText = "--";

    [ObservableProperty]
    private string _gpuUsageText = "--";

    [ObservableProperty]
    private string _gpuTemperatureText = "--";

    [ObservableProperty]
    private string _vramUsageText = "--";

    [ObservableProperty]
    private double _vramPercentage;

    [ObservableProperty]
    private string _ramUsageText = "--";

    [ObservableProperty]
    private double _ramPercentage;

    [ObservableProperty]
    private string _downloadSpeedText = "-- Mbps";

    [ObservableProperty]
    private string _uploadSpeedText = "-- Mbps";

    [ObservableProperty]
    private string _pingText = "--";

    [ObservableProperty]
    private string _uptimeText = "--";

    public IReadOnlyList<float> CpuSparklineData
    {
        get => _cpuSparklineData;
        private set => SetProperty(ref _cpuSparklineData, value);
    }

    public IReadOnlyList<float> GpuSparklineData
    {
        get => _gpuSparklineData;
        private set => SetProperty(ref _gpuSparklineData, value);
    }

    public IReadOnlyList<float> NetworkSparklineData
    {
        get => _networkSparklineData;
        private set => SetProperty(ref _networkSparklineData, value);
    }

    public bool IsAutoStart
    {
        get => _autoStartService.IsEnabled;
        set => _autoStartService.IsEnabled = value;
    }

    private DateTime _lastPingTime = DateTime.MinValue;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateDateTime();
        UpdateMetrics();

        if ((DateTime.UtcNow - _lastPingTime).TotalSeconds >= 10)
        {
            _lastPingTime = DateTime.UtcNow;
            _ = UpdatePingAsync();
        }
    }

    private void UpdateDateTime()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm:ss");
        CurrentDate = now.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"));
        CurrentDay = now.ToString("dddd", new CultureInfo("tr-TR"));
    }

    private void UpdateMetrics()
    {
        try
        {
            _hardwareMonitor.Update();

            var cpu = _hardwareMonitor.GetCpuMetrics();
            CpuUsageText = $"%{cpu.load:F0}";
            CpuTemperatureText = cpu.temp > 0 ? $"{cpu.temp:F0}°C" : "--";

            var gpu = _hardwareMonitor.GetGpuMetrics();
            GpuUsageText = $"%{gpu.load:F0}";
            GpuTemperatureText = gpu.temp > 0 ? $"{gpu.temp:F0}°C" : "--";
            VramUsageText = gpu.vramTotal > 0
                ? $"{gpu.vramUsed:F1} / {gpu.vramTotal:F0} GB"
                : "--";
            VramPercentage = gpu.vramTotal > 0
                ? Math.Round(gpu.vramUsed / gpu.vramTotal * 100, 1)
                : 0;

            var ram = _hardwareMonitor.GetRamMetrics();
            RamUsageText = ram.totalGB > 0
                ? $"{ram.usedGB:F1} / {ram.totalGB:F0} GB"
                : "--";
            RamPercentage = ram.totalGB > 0
                ? Math.Round(ram.usedGB / ram.totalGB * 100, 1)
                : 0;

            var net = _networkMonitor.GetSpeeds();
            var downMbps = net.downloadMbps;
            DownloadSpeedText = $"{downMbps:F1} Mbps";
            UploadSpeedText = $"{net.uploadMbps:F1} Mbps";

            var uptime = _uptimeService.GetUptime();
            UptimeText = $"Sistem Açık: {_uptimeService.FormatUptime(uptime)}";

            AddHistoryPoint(cpu.load / 100f, gpu.load / 100f, (float)Math.Min(downMbps, 100));
        }
        catch { }
    }

    private async Task UpdatePingAsync()
    {
        try
        {
            var ping = await _networkMonitor.GetPingMsAsync();
            PingText = ping >= 0 ? $"{ping}ms" : "--";
        }
        catch { }
    }

    private void AddHistoryPoint(float cpuValue, float gpuValue, float networkValue)
    {
        _cpuHistory.Add(cpuValue);
        _gpuHistory.Add(gpuValue);
        _networkHistory.Add(networkValue);

        while (_cpuHistory.Count > MaxHistory)
            _cpuHistory.RemoveAt(0);
        while (_gpuHistory.Count > MaxHistory)
            _gpuHistory.RemoveAt(0);
        while (_networkHistory.Count > MaxHistory)
            _networkHistory.RemoveAt(0);

        CpuSparklineData = _cpuHistory.ToList();
        GpuSparklineData = _gpuHistory.ToList();
        NetworkSparklineData = _networkHistory.ToList();
    }

    public string? GetCurrentMonitorDeviceName()
    {
        return _displayService.GetTargetScreen()?.DeviceName;
    }
}
