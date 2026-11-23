using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using SysDash.Services;
using SysDash.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace SysDash;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel _viewModel;
    private readonly DisplayService _displayService;
    private NotifyIcon? _trayIcon;
    private readonly DispatcherTimer _screenCheckTimer;
    private bool _isWindowed;
    private bool _userOverride;
    private string _lastScreenSignature = "";

    public MainWindow(DashboardViewModel viewModel, DisplayService displayService)
    {
        _viewModel = viewModel;
        _displayService = displayService;
        DataContext = viewModel;
        InitializeComponent();

        _screenCheckTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _screenCheckTimer.Tick += OnScreenCheckTick;
        _screenCheckTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isWindowed = WindowStyle == WindowStyle.SingleBorderWindow;
        CreateTrayIcon();

        // Ilk turda imzayi yalnizca kaydet; aksi halde acilista konum/kip kendini duzeltir.
        _lastScreenSignature = _displayService.CheckForScreenChanges();

        // Pencere gizli/kucultulmusken olcum durur; gereksiz CPU ve RAM harcanmaz.
        StateChanged += (_, _) => UpdateSamplingState();
        IsVisibleChanged += (_, _) => UpdateSamplingState();
        UpdateSamplingState();

        // Kucuk pencerede boyut/konum degisikligini hatirla (surukleme bitince).
        SizeChanged += (_, _) => RememberOverlayBounds();
        LocationChanged += (_, _) => RememberOverlayBounds();
    }

    private void UpdateSamplingState()
    {
        var visible = IsVisible && WindowState != WindowState.Minimized;
        _viewModel.SetSamplingPaused(!visible);

        // Gorunmeyen pencerede proc calisma kumesini OS'e iade et (WPF/DirectX yuzeyleri).
        if (!visible)
            Services.MemoryTrimmer.Trim();
    }

    private void RememberOverlayBounds()
    {
        if (!string.Equals(_displayService.CurrentMode, "overlay", StringComparison.OrdinalIgnoreCase))
            return;
        if (WindowState != WindowState.Normal) return;
        _displayService.RememberOverlayBounds(Left, Top, Width, Height);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Log.Info("Pencere kapaniyor.");
        _trayIcon?.Dispose();
        _screenCheckTimer.Stop();
    }

    /// <summary>
    /// Ekran sayisi degil, EKRAN KIMLIGI degisirse tepki ver. Onceden yalniz sayi bakildigi icin
    /// "3 sn sonra kendini eski haline getiriyor" hatasi olusuyordu: tepside secilen ekran/kucuk
    /// pencere 3 sn'de bir bu tikle geri aliniyordu.
    /// </summary>
    private void OnScreenCheckTick(object? sender, EventArgs e)
    {
        var current = _displayService.CheckForScreenChanges();
        if (current == _lastScreenSignature) return;
        _lastScreenSignature = current;

        // Kullanici tepside ekran/gorunum sectiyse otomatik yerlestirme yapma.
        if (_userOverride) return;

        var screens = Screen.AllScreens;
        if (screens.Length > 1)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(handle);
            var target = _displayService.GetSmallestScreen();
            if (target != null && !string.Equals(target.DeviceName, currentScreen.DeviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _displayService.PositionWindowOnScreen(this, target);
                _isWindowed = false;
                BuildTrayMenu();
            }
        }
        else if (!_isWindowed)
        {
            _displayService.SetWindowedMode(this);
            _isWindowed = true;
            BuildTrayMenu();
        }
        Log.Info($"Ekran duzeni degisti -> yeniden yerlestirildi (ekran sayisi {screens.Length}).");
    }

    private void OnDpiChanged(object sender, System.Windows.DpiChangedEventArgs e)
    {
        var target = _isWindowed ? null : _displayService.GetTargetScreen();
        if (target != null)
            _displayService.PositionWindowOnScreen(this, target);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "SysDash.exe"),
            Text = "SysDash",
            Visible = true
        };

        BuildTrayMenu();

        _trayIcon.DoubleClick += (_, _) =>
        {
            Show();
            Activate();
            WindowState = WindowState.Normal;
        };
    }

    private void BuildTrayMenu()
    {
        if (_trayIcon == null) return;
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var monitorMenu = new ToolStripMenuItem("Ekran Seç");
        var screens = Screen.AllScreens;
        var currentDevice = _viewModel.GetCurrentMonitorDeviceName();

        foreach (var screen in screens)
        {
            var label = $"{screen.DeviceName} - {screen.Bounds.Width}x{screen.Bounds.Height}";
            var item = new ToolStripMenuItem(label)
            {
                Checked = screen.DeviceName.Equals(currentDevice, StringComparison.OrdinalIgnoreCase)
            };
            var capturedScreen = screen;
            item.Click += (_, _) =>
            {
                _userOverride = true;          // otomatik yerlestirme bir daha ezmesin
                _displayService.SetTargetScreen(capturedScreen.DeviceName);
                _displayService.PositionWindowOnScreen(this, capturedScreen);
                _isWindowed = false;
                BuildTrayMenu();
                Log.Info($"Ekran secildi: {capturedScreen.DeviceName}");
            };
            monitorMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(monitorMenu);
        menu.Items.Add(new ToolStripSeparator());

        // Panel / overlay secimi: overlay kucuk pencere, ilk acilista boyutu ayarlanir.
        var modeMenu = new ToolStripMenuItem("Görünüm");
        foreach (var (key, label) in new[] { ("panel", "Tam Ekran Panel"), ("overlay", "Küçük Pencere (düşük RAM)") })
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = string.Equals(_displayService.CurrentMode, key, StringComparison.OrdinalIgnoreCase)
            };
            var capturedKey = key;
            item.Click += (_, _) =>
            {
                _userOverride = true;          // kullanici kipi sectiyse otomatik yerlestirme kapali
                _displayService.SetMode(capturedKey);
                _displayService.ApplyMode(this);
                _isWindowed = true;
                BuildTrayMenu();
                Log.Info($"Gorunum kipi secildi: {capturedKey}");
            };
            modeMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(modeMenu);
        menu.Items.Add(new ToolStripSeparator());

        var autoStartItem = new ToolStripMenuItem("Başlangıçta Aç")
        {
            Checked = _viewModel.IsAutoStart
        };
        autoStartItem.Click += (_, _) =>
        {
            _viewModel.IsAutoStart = !_viewModel.IsAutoStart;
            autoStartItem.Checked = _viewModel.IsAutoStart;
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = menu.Items.Add("Çıkış");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        };

        _trayIcon.ContextMenuStrip = menu;
    }
}
