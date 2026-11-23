using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;

namespace SysDash.Services;

/// <summary>
/// Surucu harfi olmayan bolumleri (ornegin Linux ext4 kurulumlari, Windows'ta gorunmez)
/// okuyup doluluk raporlar.
///
/// Windows bu bolumlere mektup vermedigi icin normal API'ler (DriveInfo) erisemez.
/// Dosya sistemi surucusu yok; tek yol ham disk okumak ve bunun icin YONETICI hakki gerekir.
/// Bu yuzden is, yonetici roluyle ayri bir surecte yapilir ve sonuc JSON olarak doner.
/// Yonetici izni verilmezse bolum yine listelenir, yalnizca doluluk yerine boyut gosterilir.
/// </summary>
public static class UnmountedPartitionService
{
    /// <summary>GPT turu: Linux filesystem data.</summary>
    private const string LinuxGptType = "0fc63daf-8483-4772-8e79-3d69d8477de4";

    public sealed record RawPartition(int DiskNumber, int PartitionNumber, ulong Size, ulong Offset);

    /// <summary>Harfsiz Linux bolumlerini listeler (yonetici gerektirmez).</summary>
    public static List<RawPartition> FindLinuxPartitions()
    {
        var result = new List<RawPartition>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DiskNumber, PartitionNumber, Size, Offset, GptType FROM MSFT_Partition");

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                var gpt = mo["GptType"]?.ToString()?.Trim();
                if (gpt == null || !gpt.Trim('{', '}').Equals(LinuxGptType, StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(new RawPartition(
                    Convert.ToInt32(mo["DiskNumber"]),
                    Convert.ToInt32(mo["PartitionNumber"]),
                    Convert.ToUInt64(mo["Size"]),
                    Convert.ToUInt64(mo["Offset"])));
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Linux bolumleri listelenemedi.", ex);
        }

        return result;
    }

    /// <summary>ext4 superblock'tan toplam/bos GB. null = okunamadi (yonetici izni yok).</summary>
    public static (double totalGb, double freeGb, string label)? TryReadExt4(RawPartition part)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Tools", "LinuxPartRead.py");
        if (!File.Exists(script))
        {
            Log.Warn($"Linux bolum okuyucu bulunamadi: {script}");
            return null;
        }

        var resultFile = Path.Combine(Path.GetTempPath(), $"sysdash_linux_{part.DiskNumber}_{part.PartitionNumber}.json");
        try
        {
            if (File.Exists(resultFile)) File.Delete(resultFile);
        }
        catch { }

        try
        {
            // Dogrudan "py" baslatmak yonetici penceresinde bash/cmd yoksa sessizce olur; bat ile sar.
            // Bat dosyasini ETKILESIMSIZ hash'li bir ada yazma: yonetici sureci gec baslarsa
            // dosya silinmis olabilir. Sabit ad + her seferinde ustune yaz daha guvenilir.
            var bat = Path.Combine(Path.GetTempPath(), "sysdash_linux_read.bat");
            var logFile = Path.Combine(Path.GetTempPath(), "sysdash_linux_read.log");
            File.WriteAllText(bat,
                "@echo off\r\n" +
                $"py -3 \"{script}\" {part.Offset} {part.DiskNumber} {part.Size} \"{resultFile}\" > \"{logFile}\" 2>&1\r\n");

            Log.Info($"Linux bolum okuma baslatiliyor: disk {part.DiskNumber}, offset {part.Offset}");

            // ShellExecuteW "runas": UAC istemi cikar. Kullanici reddederse <32 doner.
            var code = ShellExecute(IntPtr.Zero, "runas", "cmd.exe", $"/c \"{bat}\"", null, 0);
            if (code <= 32)
            {
                Log.Warn($"Linux bolum okuma izni verilmedi (ShellExecute={code}).");
                return null;
            }

            // Yonetici sureci bitene kadar kisa sure bekle (en fazla 4 sn).
            for (var i = 0; i < 40; i++)
            {
                if (File.Exists(resultFile))
                {
                    var json = File.ReadAllText(resultFile);
                    var doc = JsonSerializer.Deserialize<JsonElement>(json);
                    if (!doc.TryGetProperty("ok", out var okEl) || !okEl.GetBoolean())
                        return null;

                    return (doc.GetProperty("total_gb").GetDouble(),
                            doc.GetProperty("free_gb").GetDouble(),
                            doc.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "");
                }
                Thread.Sleep(100);
            }

            Log.Warn("Linux bolum okuma zaman asimina ugradi. " +
                     (File.Exists(Path.Combine(Path.GetTempPath(), "sysdash_linux_read.log"))
                         ? File.ReadAllText(Path.Combine(Path.GetTempPath(), "sysdash_linux_read.log"))
                         : "log yok"));
            return null;
        }
        catch (Exception ex)
        {
            Log.Warn("Linux bolum okuyucu calistirilamadi.", ex);
            return null;
        }
        finally
        {
            try { if (File.Exists(resultFile)) File.Delete(resultFile); } catch { }
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string verb, string file, string? parameters,
        string? directory, int showCmd);
}
