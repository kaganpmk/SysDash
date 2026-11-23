// Kalici: <see cref="Startup.OsTrim"/> icin yardimci.
// Windows, WPF penceresine ayrilan DirectX yuzeylerinin bir kismini calisma kumesinden
// temizlemez. Pencere gorunur kaldigi surece bunlar gercekten kullanilir; ama gizlendiginde
// veya kucultuldugunde OS'e geri verilmeli. Aksi halde 300 MB'lik bir monitor surekli RAM isgal eder.
using System;
using System.Diagnostics;
using System.Runtime;

namespace SysDash.Services;

public static class MemoryTrimmer
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr min, IntPtr max);

    /// <summary>
    /// Buyuk nesne yigini sikistirilip calisma kumesi OS'a iade edilir.
    /// Gizli/kucultulmus pencerede cagrilir; gorunur pencerede CAĞIRMA (yeniden render maliyeti).
    /// </summary>
    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            using var p = Process.GetCurrentProcess();
            SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
            Log.Info($"Bellek iade edildi, ws={Environment.WorkingSet / 1048576} MB.");
        }
        catch (Exception ex)
        {
            Log.Warn("Bellek iadesi basarisiz.", ex);
        }
    }
}
