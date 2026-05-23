using CommunityToolkit.Mvvm.ComponentModel;

namespace IpadScreen.Models;

public partial class SystemMetrics : ObservableObject
{
    public float CpuUsage { get; set; }
    public float CpuTemperature { get; set; }
    public float GpuUsage { get; set; }
    public float GpuTemperature { get; set; }
    public float VramUsed { get; set; }
    public float VramTotal { get; set; }
    public float RamUsed { get; set; }
    public float RamTotal { get; set; }
    public float DownloadSpeed { get; set; }
    public float UploadSpeed { get; set; }
    public int PingMs { get; set; }
    public TimeSpan Uptime { get; set; }
    public List<float> CpuHistory { get; set; } = new();
    public List<float> GpuHistory { get; set; } = new();
}
