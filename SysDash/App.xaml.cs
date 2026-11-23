using SysDash.Services;
using SysDash.ViewModels;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace SysDash;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Sessiz cokme yasak: yakalanmayan istisnalar ve acilis modu loga yazilir.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Yakalanmamis istisna", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Gozlenmeyen gorev istisnasi", args.Exception);
            args.SetObserved();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Dispatcher istisnasi", args.Exception);
            args.Handled = true;
        };

        Log.Info($"Acilis: baslangic ({Environment.Version}, {RuntimeInformation.ProcessArchitecture})");

        var configService = new ConfigService();
        var displayService = new DisplayService(configService);
        var autoStartService = new AutoStartService();
        var hardwareMonitor = new HardwareMonitorService();
        var networkMonitor = new NetworkMonitorService();
        var uptimeService = new UptimeService();

        networkMonitor.SetTargets(configService.Load().PingTargets);
        networkMonitor.Initialize();

        var viewModel = new DashboardViewModel(
            hardwareMonitor, networkMonitor, uptimeService,
            autoStartService, displayService, configService);

        var mainWindow = new MainWindow(viewModel, displayService);

        // Cikista LHM/DirectX kaynaklarini dogru birak.
        Exit += (_, _) =>
        {
            viewModel.SetSamplingPaused(true);
            hardwareMonitor.Dispose();
            Log.Info("Uygulama kapandi.");
        };

        var screenCount = Screen.AllScreens.Length;
        Log.Info($"Ekran sayisi: {screenCount}, gorunum kipi: {displayService.CurrentMode}");

        // Kip config'ten gelir: panel (tam ekran) veya overlay (kucuk pencere, dusuk RAM).
        displayService.ApplyMode(mainWindow);

        mainWindow.Show();
        Log.Info($"Acilis tamam: {mainWindow.Width:F0}x{mainWindow.Height:F0} @ {mainWindow.Left:F0},{mainWindow.Top:F0}");

        base.OnStartup(e);
    }
}
