using System.IO;

namespace SysDash.Services;

public sealed record DiskInfo(string Letter, string Label, float UsedGb, float TotalGb, float Percent)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Label) ? $"{Letter}" : $"{Letter} · {Label}";
}

/// <summary>
/// Disk doluluklari. WMI'ya her turda gidilmez: sabit disk listesi bir kez cozulur,
/// doluluk DriveInfo'dan okunur (WMI'ya gore ~100x ucuz), 5 turda bir tazelenir.
/// </summary>
public class DiskMonitorService
{
    private const int RefreshEveryTicks = 5;
    private readonly List<DiskInfo> _disks = new();
    private readonly Dictionary<int, (double totalGb, double freeGb, string label)?> _linuxCache = new();
    private List<UnmountedPartitionService.RawPartition>? _linuxParts;
    private int _tick;

    public IReadOnlyList<DiskInfo> Disks => _disks;

    public void Refresh()
    {
        if (_tick++ % RefreshEveryTicks != 0 && _disks.Count > 0) return;

        try
        {
            var list = new List<DiskInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                if (drive.TotalSize <= 0) continue;

                var total = drive.TotalSize / 1024f / 1024f / 1024f;
                var free = drive.TotalFreeSpace / 1024f / 1024f / 1024f;
                // Sistem kurtarma bolumleri (<=1 GB) panele girmesin.
                if (total < 2) continue;

                var used = total - free;
                list.Add(new DiskInfo(
                    drive.Name.TrimEnd('\\'),
                    string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "" : drive.VolumeLabel,
                    used, total, total > 0 ? used / total * 100f : 0));
            }

            // Surucu harfi olmayan bolumler (ornegin Linux kurulumu) ayrica eklenir.
            // Bolum listesi ve doluluk bir kez okunur (UAC her turda cikmasin).
            if (_linuxParts == null)
            {
                _linuxParts = UnmountedPartitionService.FindLinuxPartitions();
                Log.Info($"Harfsiz Linux bolumu: {_linuxParts.Count} adet.");
            }

            foreach (var part in _linuxParts)
            {
                var read = _linuxCache.TryGetValue(part.PartitionNumber, out var cached)
                    ? cached
                    : UnmountedPartitionService.TryReadExt4(part);
                if (read != null) _linuxCache[part.PartitionNumber] = read;
                if (read is not { } r || r.totalGb <= 0)
                {
                    // Doluluk okunamadi ama bolum gercek: yalnizca boyutu goster.
                    list.Add(new DiskInfo($"Linux {part.Size / 1024.0 / 1024 / 1024:F0}GB",
                        "ext4", 0, (float)(part.Size / 1024.0 / 1024 / 1024), -1));
                    continue;
                }

                var used = (float)(r.totalGb - r.freeGb);
                // Ad: bolum etiketi varsa o, yoksa "Linux". DisplayName "ad · etiket" yazar,
                // burada "ext4" etiket olarak gecilirse ad "Linux - ext4" gibi karisir.
                var name = string.IsNullOrWhiteSpace(r.label) ? "Linux" : r.label.Trim();
                list.Add(new DiskInfo(name, "",
                    used, (float)r.totalGb,
                    (float)(used / r.totalGb * 100)));
            }

            _disks.Clear();
            _disks.AddRange(list.OrderBy(d => d.Letter, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.Warn("Disk bilgisi okunamadi.", ex);
        }
    }
}
