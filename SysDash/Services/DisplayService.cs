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
    private string _lastScreenSignature = "";

    /// <summary>
    /// Ekran duzeninin imzasi: cihaz adi + cozunurluk + konum. Sayi degil KIMLIK karsilastirilir,
    /// aksi halde ayni sayida ekranla ekran degistirmek algilanmaz.
    /// </summary>
    public string CheckForScreenChanges()
    {
        var screens = Screen.AllScreens;
        var signature = string.Join("|", screens.Select(s =>
            $"{s.DeviceName}:{s.Bounds.Width}x{s.Bounds.Height}@{s.Bounds.X},{s.Bounds.Y}"));

        if (signature != _lastScreenSignature)
        {
            var changed = _lastScreenSignature.Length > 0;
            _lastScreenSignature = signature;
            _lastKnownScreenCount = screens.Length;
            if (changed)
            {
                Log.Info($"Ekran duzeni: {signature}");
                ScreensChanged?.Invoke();
            }
        }
        return signature;
    }

    public string CurrentMode => string.IsNullOrEmpty(_config.Mode) ? "panel" : _config.Mode;

    /// <summary>Kucuk pencere konumu/boyutu hatirlanir; sonraki acilista oraya gelir.</summary>
    public void RememberOverlayBounds(double left, double top, double width, double height)
    {
        if (double.IsNaN(left) || double.IsNaN(top) || width < 100 || height < 100) return;
        // Kucultulmus pencere -32000'de durur; bu konumu kaydetmek sonraki acilisi bozar.
        if (left <= -30000 || top <= -30000) return;
        if (_config.OverlayLeft == left && _config.OverlayTop == top &&
            _config.OverlayWidth == width && _config.OverlayHeight == height) return;

        _config.OverlayLeft = left;
        _config.OverlayTop = top;
        _config.OverlayWidth = width;
        _config.OverlayHeight = height;
        _configService.Save(_config);
    }

    public void SetMode(string mode)
    {
        _config.Mode = mode;
        _configService.Save(_config);
        Log.Info($"Gorunum kipi: {mode}");
    }

    /// <summary>
    /// Config'teki kipe gore pencereyi yerlestirir.
    /// OLCUM (2026-11-23): kucuk pencere RAM'i DUSURMUYOR (420x300 de 200x150 de ~298 MB).
    /// Maliyet pencere boyutunda degil, WPF'in tum XAML agacini kurmasindan geliyor.
    /// Bu yuzden overlay yalniz bir rahatlik secenegidir; RAM icin ayri bir sade gorunum gerekir.
    /// </summary>
    public void ApplyMode(Window window)
    {
        if (string.Equals(CurrentMode, "overlay", StringComparison.OrdinalIgnoreCase))
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize;
            window.WindowState = WindowState.Normal;
            window.Topmost = true;
            window.Title = "SysDash";

            // Ilk acilista ekranin sag ust kosesine yerlestir; sonra kullanici konumunu hatirla.
            var area = SystemParameters.WorkArea;
            // Varsayilan 800x600; kullanici boyutlandirinca config'e yazilir.
            // Varsayilan 800x600; kullanici boyutlandirdiysa o deger kullanilir (0 = varsayilan).
            window.Width = _config.OverlayWidth >= 300 ? _config.OverlayWidth : 800;
            window.Height = _config.OverlayHeight >= 200 ? _config.OverlayHeight : 600;
            // Kayitli konum yoksa ya da ekran disindaysa sag ust koseye yerles.
            var left = _config.OverlayLeft ?? double.NaN;
            var top = _config.OverlayTop ?? double.NaN;
            var onScreen = !double.IsNaN(left) && !double.IsNaN(top) &&
                           left > area.Left - 50 && top > area.Top - 50 &&
                           left < area.Right - 100 && top < area.Bottom - 100;

            window.Left = onScreen ? left : Math.Max(area.Left + 24, area.Right - window.Width - 24);
            window.Top = onScreen ? top : area.Top + 24;
            Log.Info($"Overlay modu: {window.Width:F0}x{window.Height:F0} @ {window.Left:F0},{window.Top:F0}");
            return;
        }

        // panel: cok ekran varsa hedef ekran tam kaplansin, yoksa pencere modu.
        var screens = Screen.AllScreens;
        if (screens.Length > 1)
        {
            var target = GetTargetScreen();
            if (target != null) PositionWindowOnScreen(window, target);
        }
        else
        {
            SetWindowedMode(window);
        }
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
