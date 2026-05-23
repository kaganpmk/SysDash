using IpadScreen.Services;
using IpadScreen.ViewModels;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace IpadScreen;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var configService = new ConfigService();
        var displayService = new DisplayService(configService);
        var autoStartService = new AutoStartService();
        var hardwareMonitor = new HardwareMonitorService();
        var networkMonitor = new NetworkMonitorService();
        var uptimeService = new UptimeService();

        networkMonitor.Initialize();

        var viewModel = new DashboardViewModel(
            hardwareMonitor, networkMonitor, uptimeService,
            autoStartService, displayService, configService);

        var mainWindow = new MainWindow(viewModel, displayService);
        var screenCount = Screen.AllScreens.Length;

        if (screenCount > 1)
        {
            var target = displayService.GetSmallestScreen();
            if (target != null)
                displayService.PositionWindowOnScreen(mainWindow, target);
        }
        else
        {
            displayService.SetWindowedMode(mainWindow);
        }

        mainWindow.Show();

        base.OnStartup(e);
    }
}
