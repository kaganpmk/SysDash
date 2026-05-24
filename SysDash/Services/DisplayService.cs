using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace SysDash.Services;

public class DisplayService
{
    private readonly ConfigService _configService;
    private Config _config;

    public DisplayService(ConfigService configService)
    {
        _configService = configService;
        _config = configService.Load();
    }

    public event Action? ScreensChanged;

    public int ScreenCount => Screen.AllScreens.Length;

    public System.Windows.Forms.Screen? GetSmallestScreen()
    {
        return Screen.AllScreens.OrderBy(s => s.Bounds.Width * s.Bounds.Height).FirstOrDefault();
    }

    public System.Windows.Forms.Screen? GetTargetScreen()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0) return null;

        if (!string.IsNullOrEmpty(_config.SelectedMonitor))
        {
            var saved = screens.FirstOrDefault(s =>
                s.DeviceName.Equals(_config.SelectedMonitor, StringComparison.OrdinalIgnoreCase));
            if (saved != null) return saved;
        }

        return GetSmallestScreen();
    }

    public void SetTargetScreen(string deviceName)
    {
        _config.SelectedMonitor = deviceName;
        _configService.Save(_config);
    }

    public System.Windows.Forms.Screen[] GetAllScreens() => Screen.AllScreens;

    public System.Windows.Forms.Screen? GetScreenByDeviceName(string deviceName)
    {
        return Screen.AllScreens.FirstOrDefault(s =>
            s.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
    }

    public void PositionWindowOnScreen(Window window, System.Windows.Forms.Screen screen)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowState = WindowState.Normal;
        window.Topmost = true;

        var bounds = screen.Bounds;
        var (scaleX, scaleY) = GetDpiScale(screen);
        window.Left = bounds.X / scaleX;
        window.Top = bounds.Y / scaleY;
        window.Width = bounds.Width / scaleX;
        window.Height = bounds.Height / scaleY;
    }

    private const int LOGPIXELSX = 88;
    private const int LOGPIXELSY = 90;

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    private static (double scaleX, double scaleY) GetDpiScale(System.Windows.Forms.Screen screen)
    {
        var dc = CreateDC(screen.DeviceName, null, null, IntPtr.Zero);
        if (dc == IntPtr.Zero) return (1.0, 1.0);

        var dpiX = GetDeviceCaps(dc, LOGPIXELSX);
        var dpiY = GetDeviceCaps(dc, LOGPIXELSY);
        DeleteDC(dc);

        return (dpiX / 96.0, dpiY / 96.0);
    }

    private int _lastKnownScreenCount = Screen.AllScreens.Length;

    public int CheckForScreenChanges()
    {
        var current = Screen.AllScreens.Length;
        if (current != _lastKnownScreenCount)
        {
            _lastKnownScreenCount = current;
            ScreensChanged?.Invoke();
        }
        return current;
    }

    public void SetWindowedMode(Window window)
    {
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.WindowState = WindowState.Normal;
        window.Topmost = false;
        window.Title = "SysDash";
        window.Left = (SystemParameters.WorkArea.Width - 800) / 2;
        window.Top = (SystemParameters.WorkArea.Height - 600) / 2;
        window.Width = 800;
        window.Height = 600;
    }
}
