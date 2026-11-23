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
    private readonly DiskMonitorService _diskMonitor = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _metricsTimer;

    // Ping ayri sayacta: metrik turuna baglamak %3'luk gecikme yaratiyordu.
    private readonly DispatcherTimer _pingTimer;
    private bool _pingBusy;

    // Pencere gizliyken olcum durur; gorunur olunca tek tur atilip devam eder.
    private bool _samplingPaused;

    private static readonly CultureInfo Tr = new("tr-TR");

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

        _pingTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _pingTimer.Tick += OnPingTick;
        _pingTimer.Start();

        UpdateDateTime();
        UpdateMetrics();
        _ = UpdatePingAsync();
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

    /// <summary>VRAM kullanim verisi guvenilir mi? LHM kullanim sensoru vermiyorsa false olur.</summary>
    [ObservableProperty]
    private bool _vramAvailable;

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

    /// <summary>Ping tooltip'i: hangi hedefe olcuyor (kullsanicinin sikayeti: "bu pingi neye gore aliyor").</summary>
    public string PingTargetsLabel => _networkMonitor.TargetsLabel;

    /// <summary>Disk seridi: "C: · Windows  247/512 GB" + yuzde. XAML'de ItemsControl baglar.</summary>
    public System.Collections.ObjectModel.ObservableCollection<DiskRow> Disks { get; } = new();

    public bool IsAutoStart
    {
        get => _autoStartService.IsEnabled;
        set => _autoStartService.IsEnabled = value;
    }

    /// <summary>Pencere gorunurlugune gore olcumu durdurur/devam ettirir.</summary>
    public void SetSamplingPaused(bool paused)
    {
        if (_samplingPaused == paused) return;
        _samplingPaused = paused;

        if (paused)
        {
            _metricsTimer.Stop();
            _pingTimer.Stop();
            Log.Info("Olcum duraklatildi (pencere gizli/arkada).");
        }
        else
        {
            _metricsTimer.Start();
            _pingTimer.Start();
            UpdateMetrics();
            Log.Info("Olcum devam ediyor (pencere gorunur).");
        }
    }

    private void OnMetricsTick(object? sender, EventArgs e)
    {
        UpdateMetrics();
    }

    private void OnPingTick(object? sender, EventArgs e)
    {
        _ = UpdatePingAsync();
    }

    private void UpdateDateTime()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        CurrentDate = now.ToString("dd MMMM yyyy", Tr);
        CurrentDay = now.ToString("dddd", Tr);
    }

    private void UpdateMetrics()
    {
        try
        {
            // Her turda tazele: sensorden okunan deger ancak Update() sonrasi guncel olur.
            _hardwareMonitor.Update();

            var cpu = _hardwareMonitor.GetCpuMetrics();
            CpuUsageText = $"%{cpu.load:F0}";
            CpuTemperatureText = cpu.temp > 0
                ? cpu.temp.ToString("F0", CultureInfo.CurrentCulture) + "°C"
                : "--";
            CpuPercentage = Math.Round(cpu.load, 1);

            var gpu = _hardwareMonitor.GetGpuMetrics();
            GpuUsageText = $"%{gpu.load:F0}";
            GpuTemperatureText = gpu.temp > 0
                ? gpu.temp.ToString("F0", CultureInfo.CurrentCulture) + "°C"
                : "--";

            // Kullanim degeri yoksa yanlis "%0" gostermek yerine gizle.
            VramAvailable = gpu.vramUsed > 0 && gpu.vramTotal > 0;
            VramUsageText = VramAvailable
                ? $"{gpu.vramUsed:F1} / {gpu.vramTotal:F0} GB"
                : "--";
            VramPercentage = VramAvailable
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

            UpdateDisks();
        }
        catch (Exception ex)
        {
            Log.Error("Olcum turu basarisiz.", ex);
        }
    }

    private void UpdateDisks()
    {
        _diskMonitor.Refresh();
        var source = _diskMonitor.Disks;

        // Koleksiyonu her turda yeniden kurma: ayni sayida disk varsa degerleri guncelle.
        if (Disks.Count != source.Count)
        {
            Disks.Clear();
            foreach (var d in source) Disks.Add(DiskRow.From(d));
            return;
        }

        for (var i = 0; i < source.Count; i++)
            Disks[i].Update(source[i]);
    }

    private async Task UpdatePingAsync()
    {
        if (_pingBusy) return;
        _pingBusy = true;
        try
        {
            var ping = await _networkMonitor.GetPingMsAsync();
            PingText = ping >= 0 ? $"{ping}ms" : "--";
        }
        catch (Exception ex)
        {
            PingText = "--";
            Log.Warn("Ping olcumu basarisiz.", ex);
        }
        finally
        {
            _pingBusy = false;
        }
    }

    public string? GetCurrentMonitorDeviceName()
    {
        return _displayService.GetTargetScreen()?.DeviceName;
    }
}
