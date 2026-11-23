using System.IO;

namespace SysDash.Services;

/// <summary>
/// Tek satirlik dosya log'u: <c>%APPDATA%\SysDash\startup.log</c>.
/// Kural: yakalanan istisnalar sessizce yutulmaz; en az bir satir yazilir.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SysDash");
    private static readonly string LogPath = System.IO.Path.Combine(Dir, "startup.log");
    private static long _lastTrimTicks;

    public static string FilePath => LogPath;

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERRO", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null)
                line += Environment.NewLine + "        " + ex.GetType().Name + ": " + ex.Message +
                        (ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "");

            lock (Gate)
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);

                // 1 MB'i asinca kirp: son ~200 KB kalsin.
                var now = Environment.TickCount64;
                if (now - _lastTrimTicks > 60_000)
                {
                    _lastTrimTicks = now;
                    var info = new FileInfo(LogPath);
                    if (info.Exists && info.Length > 1024 * 1024)
                    {
                        var keep = 200 * 1024;
                        using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        fs.Seek(-keep, SeekOrigin.End);
                        using var reader = new StreamReader(fs);
                        var tail = reader.ReadToEnd();
                        File.WriteAllText(LogPath, "--- kisaltildi ---" + Environment.NewLine + tail);
                    }
                }

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Log yazilamiyorsa yapabilecek bir sey yok; sessiz cokme yasak ama log da cokmemeli.
        }
    }
}
