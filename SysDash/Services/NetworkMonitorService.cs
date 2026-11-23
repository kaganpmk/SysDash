using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SysDash.Services;

public class NetworkMonitorService
{
    // Birden fazla hedef: tek DNS bloklu aglarda ping "--" da kalmasin.
    // Hedefler config.json'daki "PingTargets" listesinden gelir; varsayilan Cloudflare + Google DNS.
    private string[] _pingTargets = { "1.1.1.1", "8.8.8.8" };

    public void SetTargets(string[]? targets)
    {
        if (targets is { Length: > 0 })
        {
            _pingTargets = targets;
            Log.Info($"Ping hedefleri: {string.Join(", ", targets)}");
        }
    }

    /// <summary>UI'da tooltip'te gosterilir: ping gercekte kime gidiyor.</summary>
    public string TargetsLabel => string.Join(", ", _pingTargets);
    private const int PingTimeoutMs = 2000;
    private const int RebindEveryTicks = 20; // ~60 sn'de bir arayuzu yeniden sec

    private long _prevBytesReceived;
    private long _prevBytesSent;
    private DateTime _prevTime;
    private NetworkInterface? _networkInterface;
    private readonly Ping _ping = new();
    private bool _initialized;
    private int _ticksSinceRebind;

    public void Initialize()
    {
        _networkInterface = GetActiveInterface();
        if (_networkInterface != null)
        {
            var (rx, tx) = ReadCounters(_networkInterface);
            _prevBytesReceived = rx;
            _prevBytesSent = tx;
            _prevTime = DateTime.UtcNow;
            _initialized = true;
            _ticksSinceRebind = 0;
            Log.Info($"Ag arayuzu: {_networkInterface.Name} ({_networkInterface.Speed / 1_000_000} Mbps)");
        }
        else
        {
            _initialized = false;
            Log.Warn("Aktif ag arayuzu bulunamadi.");
        }
    }

    /// <summary>Arayuzun toplam byte sayaclari.</summary>
    private static (long rx, long tx) ReadCounters(NetworkInterface ni)
    {
        long rx = 0, tx = 0;
        try
        {
            var v4 = ni.GetIPv4Statistics();
            rx += v4.BytesReceived;
            tx += v4.BytesSent;
        }
        catch { }
        return (rx, tx);
    }

    public (float downloadMbps, float uploadMbps) GetSpeeds()
    {
        if (!_initialized || _networkInterface == null)
        {
            Initialize();
            return (0, 0);
        }

        try
        {
            // Wi-Fi <-> Ethernet gecisi, adaptor kapanmasi: arayuzu periyodik olarak yeniden sec.
            if (++_ticksSinceRebind >= RebindEveryTicks)
            {
                _ticksSinceRebind = 0;
                if (_networkInterface.OperationalStatus != OperationalStatus.Up)
                    Initialize();
            }

            var (rx, tx) = ReadCounters(_networkInterface);
            var now = DateTime.UtcNow;
            var elapsed = (now - _prevTime).TotalSeconds;

            if (elapsed < 0.1) return (0, 0);

            var bytesRecv = rx - _prevBytesReceived;
            var bytesSent = tx - _prevBytesSent;

            _prevBytesReceived = rx;
            _prevBytesSent = tx;
            _prevTime = now;

            // Sayac sifirlanirsa (adaptor yeniden basladi) negatif delta olusur; 0 kabul et.
            if (bytesRecv < 0) bytesRecv = 0;
            if (bytesSent < 0) bytesSent = 0;

            var downloadMbps = (float)(bytesRecv * 8.0 / (1024 * 1024) / elapsed);
            var uploadMbps = (float)(bytesSent * 8.0 / (1024 * 1024) / elapsed);

            return (Math.Max(0, downloadMbps), Math.Max(0, uploadMbps));
        }
        catch (Exception ex)
        {
            Log.Warn("Ag hizi okunamadi.", ex);
            return (0, 0);
        }
    }

    public async Task<int> GetPingMsAsync()
    {
        foreach (var target in _pingTargets)
        {
            try
            {
                var reply = await _ping.SendPingAsync(target, PingTimeoutMs).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                    return (int)reply.RoundtripTime;
            }
            catch (Exception ex)
            {
                Log.Warn($"Ping basarisiz: {target}", ex);
            }
        }
        return -1;
    }

    private static NetworkInterface? GetActiveInterface()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(n => n.Speed)
            .ToArray();

        if (interfaces.Length == 0) return null;

        foreach (var ni in interfaces)
        {
            try
            {
                var stats = ni.GetIPv4Statistics();
                if (stats.BytesReceived > 0 || stats.BytesSent > 0)
                    return ni;
            }
            catch { }
        }

        return interfaces[0];
    }
}
