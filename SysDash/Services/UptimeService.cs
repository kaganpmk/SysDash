using System.Runtime.InteropServices;

namespace SysDash.Services;

public class UptimeService
{
    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    public TimeSpan GetUptime()
    {
        var ms = GetTickCount64();
        return TimeSpan.FromMilliseconds(ms);
    }

    public string FormatUptime(TimeSpan uptime)
    {
        var parts = new List<string>();

        if (uptime.Days > 0)
            parts.Add($"{uptime.Days} gün");
        if (uptime.Hours > 0)
            parts.Add($"{uptime.Hours} saat");
        if (uptime.Minutes > 0)
            parts.Add($"{uptime.Minutes} dakika");

        if (parts.Count == 0)
            return "1 dakika";

        return string.Join(", ", parts);
    }
}
