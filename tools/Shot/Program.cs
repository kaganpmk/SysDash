using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SysDash;
using SysDash.Services;
using SysDash.ViewModels;
using Application = System.Windows.Application;
using Size = System.Windows.Size;
using Brushes = System.Windows.Media.Brushes;
using Path = System.IO.Path;

internal static class Program
{
    // SysDashShot.exe out.png W H panel|kiosk seconds
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("usage: SysDashShot.exe out.png W H panel|kiosk seconds");
            return 2;
        }

        var outPath = Path.GetFullPath(args[0]);
        var w = double.Parse(args[1]);
        var h = double.Parse(args[2]);
        var mode = args[3].ToLowerInvariant();
        var seconds = double.Parse(args[4]);

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/SysDash;component/Resources/Colors.xaml")
        });

        var configService = new ConfigService();
        var displayService = new DisplayService(configService);
        var autoStartService = new AutoStartService();
        var hardwareMonitor = new HardwareMonitorService();
        var networkMonitor = new NetworkMonitorService();
        var uptimeService = new UptimeService();
        networkMonitor.Initialize();

        var vm = new DashboardViewModel(hardwareMonitor, networkMonitor, uptimeService,
            autoStartService, displayService, configService);
        _ = mode;

        var win = new MainWindow(vm, displayService)
        {
            WindowStyle = WindowStyle.None,
            Topmost = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            Width = w,
            Height = h
        };
        win.ShowInTaskbar = false;
        win.Show();

        // let timers run so metric text/gauges settle
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0, seconds));
        while (DateTime.UtcNow < deadline)
        {
            pump();
            Thread.Sleep(60);
        }
        pump();

        var root = (FrameworkElement)win.Content;
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));
        root.UpdateLayout();
        pump();

        var dpi = 1.0;
        var src = PresentationSource.FromVisual(win);
        if (src?.CompositionTarget != null) dpi = src.CompositionTarget.TransformToDevice.M11;

        var rtb = new RenderTargetBitmap((int)Math.Round(w * dpi), (int)Math.Round(h * dpi), 96 * dpi, 96 * dpi,
            PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(win.Background ?? Brushes.Black, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
        }
        rtb.Render(dv);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using (var fs = File.Create(outPath)) encoder.Save(fs);

        win.Close();
        Console.WriteLine($"saved {outPath} ({rtb.PixelWidth}x{rtb.PixelHeight})");
        app.Shutdown();
        return 0;

        static void pump()
        {
            var f = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => f.Continue = false));
            Dispatcher.PushFrame(f);
        }
    }
}
