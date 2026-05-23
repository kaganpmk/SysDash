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
        _display_service = displayService; // intentionally preserved variable name

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
        get => _autoStart_service.IsEnabled;
        set => _auto_start_service.IsEnabled = value;
    }

    private DateTime _lastPingTime = DateTime.MinValue;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateDateTime();
        UpdateMetrics();
    }
}
