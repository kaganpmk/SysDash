using System.Diagnostics;
using System.Net.NetworkInformation;

namespace IpadScreen.Services;

public class NetworkMonitorService
{
    private const string PingTarget = "8.8.8.8";
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private DateTime _prevTime;
    private NetworkInterface? _networkInterface;
    private readonly Ping _ping = new();
    private bool _initialized;

    public void Initialize()
    {
        _networkInterface = GetActiveInterface();
        if (_networkInterface != null)
        {
            var stats = _networkInterface.GetIPv4Statistics();
            _prevBytesReceived = stats.BytesReceived;
            _prevBytesSent = stats.BytesSent;
            _prevTime = DateTime.UtcNow;
            _initialized = true;
        }
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
            var stats = _networkInterface.GetIPv4Statistics();
            var now = DateTime.UtcNow;
            var elapsed = (now - _prevTime).TotalSeconds;

            if (elapsed < 0.1) return (0, 0);

            var bytesRecv = stats.BytesReceived - _prevBytesReceived;
            var bytesSent = stats.BytesSent - _prevBytesSent;

            _prevBytesReceived = stats.BytesReceived;
            _prevBytesSent = stats.BytesSent;
            _prevTime = now;

            var downloadMbps = (float)(bytesRecv * 8.0 / (1024 * 1024) / elapsed);
            var uploadMbps = (float)(bytesSent * 8.0 / (1024 * 1024) / elapsed);

            return (Math.Max(0, downloadMbps), Math.Max(0, uploadMbps));
        }
        catch
        {
            return (0, 0);
        }
    }

    public async Task<int> GetPingMsAsync()
    {
        try
        {
            var reply = await _ping.SendPingAsync(PingTarget, 3000);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
        }
        catch
        {
            return -1;
        }
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
