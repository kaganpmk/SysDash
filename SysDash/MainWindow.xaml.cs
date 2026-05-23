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
        CreateTrayIcon();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _trayIcon?.Dispose();
        _screenCheckTimer.Stop();
    }

    private void OnScreenCheckTick(object? sender, EventArgs e)
    {
        var count = _displayService.CheckForScreenChanges();
        if (count > 1)
        {
            var wasWindowed = Width <= 900;
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(handle);
            var smallest = _displayService.GetSmallestScreen();
            if (smallest != null && (wasWindowed || smallest.DeviceName != currentScreen.DeviceName))
            {
                _displayService.PositionWindowOnScreen(this, smallest);
                BuildTrayMenu();
            }
        }
        else if (count == 1 && Width > 900)
        {
            _displayService.SetWindowedMode(this);
            BuildTrayMenu();
        }
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
                _displayService.SetTargetScreen(capturedScreen.DeviceName);
                _displayService.PositionWindowOnScreen(this, capturedScreen);
                BuildTrayMenu();
            };
            monitorMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(monitorMenu);
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
