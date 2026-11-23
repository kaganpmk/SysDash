using CommunityToolkit.Mvvm.ComponentModel;
using SysDash.Services;

namespace SysDash.ViewModels;

/// <summary>Disk seridi icin tek satirlik gorunum modeli.</summary>
public partial class DiskRow : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _usageText = "--";

    [ObservableProperty]
    private double _percent;

    /// <summary>Yuzde 90'i asarsa bar uyari rengine gecer (XAML'de tetikleyici kullanir).</summary>
    [ObservableProperty]
    private bool _isCritical;

    /// <summary>Doluluk okunamadi (yonetici hakki gerekli): bar bos ve soluk cizilir.</summary>
    [ObservableProperty]
    private bool _isUnknown;

    public static DiskRow From(DiskInfo info)
    {
        var row = new DiskRow();
        row.Update(info);
        return row;
    }

    /// <summary>Doluluk okunamadiysa (yuzde < 0) yalnizca toplam boyut ve ipucu gosterilir.</summary>
    public void Update(DiskInfo info)
    {
        Name = info.DisplayName;
        UsageText = info.Percent < 0
            ? $"{info.TotalGb:F0} GB"
            : $"{info.UsedGb:F0}/{info.TotalGb:F0} GB";
        Percent = info.Percent < 0 ? 0 : Math.Round(info.Percent, 1);
        IsCritical = info.Percent >= 90;
        IsUnknown = info.Percent < 0;
    }
}
