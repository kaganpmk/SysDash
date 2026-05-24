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
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _metricsTimer;

    public DashboardViewModel(HardwareMonitorService hardwareMonitor, NetworkMonitorService networkMonitor,
        UptimeService uptimeService, AutoStartService autoStartService,
        DisplayService displayService, ConfigService configService)
    {
        _hardwareMonitor = hardwareMonitor;
        _networkMonitor = networkMonitor;
        _uptimeService = uptimeService;
        _autoStartService = autoStartService;
        _displayService = displayService;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateDateTime();
        _clockTimer.Start();

        _metricsTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _metricsTimer.Tick += OnMetricsTick;
        _metricsTimer.Start();

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
    private double _cpuPercentage;

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
    private double _networkPercentage;

    [ObservableProperty]
    private string _pingText = "--";

    [ObservableProperty]
    private string _uptimeText = "--";

    public bool IsAutoStart
    {
        get => _autoStartService.IsEnabled;
        set => _autoStartService.IsEnabled = value;
    }

    private int _metricsTick;

    private void OnMetricsTick(object? sender, EventArgs e)
    {
        _metricsTick++;
        UpdateMetrics();

        if (_metricsTick % 3 == 0)
        {
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
            if (_metricsTick % 2 == 0)
                _hardwareMonitor.Update();

            var cpu = _hardwareMonitor.GetCpuMetrics();
            CpuUsageText = $"%{cpu.load:F0}";
            CpuTemperatureText = cpu.temp > 0 ? $"{cpu.temp:F0}°C" : "--";
            CpuPercentage = Math.Round(cpu.load, 1);

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
            NetworkPercentage = Math.Round(Math.Min(downMbps, 100), 1);

            var uptime = _uptimeService.GetUptime();
            UptimeText = $"Sistem Açık: {_uptimeService.FormatUptime(uptime)}";
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

    public string? GetCurrentMonitorDeviceName()
    {
        return _displayService.GetTargetScreen()?.DeviceName;
    }
}
