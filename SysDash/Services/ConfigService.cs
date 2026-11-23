using System.IO;
using System.Text.Json;

namespace SysDash.Services;

public class Config
{
    public string? SelectedMonitor { get; set; }
    public bool IsAutoStart { get; set; } = true;

    /// <summary>
    /// "panel"  = tam ekran paneller (tek monitorde calisirken varsayilan)
    /// "overlay"= kucuk, cercevesiz, suruklenebilir pencere: ~130 MB'a kadar dusuk bellek
    /// </summary>
    public string Mode { get; set; } = "panel";

    public double OverlayWidth { get; set; } = 0;
    public double OverlayHeight { get; set; } = 0;

    /// <summary>Ping olcumunde kullanilacak hedefler (sirayla denenir).</summary>
    public string[] PingTargets { get; set; } = { "1.1.1.1", "8.8.8.8" };

    /// <summary>Linux bolumleri (harfsiz okuma) icin yonetici yardimcisina izin var mi.</summary>
    public bool ReadUnmountedPartitions { get; set; } = true;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
}

public class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SysDash", "config.json");

    public Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
        }
        catch { }
        return new Config();
    }

    public void Save(Config config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
