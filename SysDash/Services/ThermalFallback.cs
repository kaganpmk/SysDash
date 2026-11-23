using System.Management;

namespace SysDash.Services;

/// <summary>
/// CPU sicakligi icin yedek yollar. LHM bu donanimda sensor degerleri null donduruyor.
/// Sirasiyla denenir:
///
/// 1. Win32_PerfFormattedData_Counters_ThermalZoneInformation — YONETICI IZNI GEREKMEZ.
///    Deger Kelvin x 10'dur (ornek: 337 -> 63.8 C). Bu makinede calistigi dogrulandi.
/// 2. MSAcpi_ThermalZoneTemperature — daha hassas ama yonetici izni ister.
///
/// Hicbiri olmazsa 0 doner ve UI "--" gosterir.
/// </summary>
public static class ThermalFallback
{
    private static int _ticksUntilRetry;
    private static float _lastValue;
    private static string _source = "";
    private static bool _warned;

    public static float TryRead()
    {
        if (_ticksUntilRetry > 0)
        {
            _ticksUntilRetry--;
            return _lastValue;
        }
        _ticksUntilRetry = 3;

        var value = ReadPerfCounters();
        if (value > 0)
        {
            Report("perf-counter", value);
            return value;
        }

        value = ReadAcpiWmi();
        if (value > 0)
        {
            Report("ACPI WMI", value);
            return value;
        }

        if (!_warned)
        {
            Log.Warn("CPU sicakligi hicbir kaynaktan okunamadi; '--' gosterilecek.");
            _warned = true;
        }
        return 0;
    }

    /// <summary>Yonetici izni gerektirmeyen yol: termal bolge perf sayaci (Kelvin x 10).</summary>
    private static float ReadPerfCounters()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Temperature, HighPrecisionTemperature, Name " +
                "FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            float best = 0;
            var seen = 0;
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                seen++;

                // Once yuksek hassasiyetli alan (Kelvin x 1000), yoksa Temperature (Kelvin x 10).
                var hp = mo["HighPrecisionTemperature"];
                var raw = mo["Temperature"];

                // Dogrulandi (2026-11-23): HighPrecisionTemperature = 3382, Temperature = 338.
                // IKISI DE Kelvin x 10'dur; 3382/10 = 338.2 K = 65.05 C.
                float celsius = 0;
                if (hp != null)
                    celsius = Convert.ToSingle(hp) / 10f - 273.15f;
                if ((celsius < 1 || celsius > 125) && raw != null)
                    celsius = Convert.ToSingle(raw) / 10f - 273.15f;

                if (celsius > 1 && celsius < 125 && celsius > best)
                    best = celsius;
            }

            _ = seen;
            return best;
        }
        catch (Exception ex)
        {
            Log.Warn("Termal bolge perf sayaci okunamadi.", ex);
            return 0;
        }
    }

    /// <summary>Daha hassas ama yonetici isteyen ACPI yolu.</summary>
    private static float ReadAcpiWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            float best = 0;
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                var raw = mo["CurrentTemperature"];
                if (raw == null) continue;
                var celsius = Convert.ToSingle(raw) / 10f - 273.15f;
                if (celsius > 1 && celsius < 125 && celsius > best)
                    best = celsius;
            }
            return best;
        }
        catch
        {
            // Yetki yok: sessiz gec, perf sayaci zaten denendi.
            return 0;
        }
    }

    private static void Report(string source, float value)
    {
        _lastValue = value;
        if (_source != source)
        {
            _source = source;
            Log.Info($"CPU sicakligi kaynagi: {source} ({value:F0}°C)");
        }
    }
}
