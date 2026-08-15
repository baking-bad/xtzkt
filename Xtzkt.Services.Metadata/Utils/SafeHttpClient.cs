using System.Net;
using System.Net.Sockets;

namespace Xtzkt.Services.Metadata.Utils;

/// <summary>
/// Http client with built-in SSRF protection (if no baseUri specified) and PooledConnectionLifetime
/// </summary>
public sealed class SafeHttpClient : IDisposable
{
    readonly HttpClient _client;

    public SafeHttpClient() : this((Uri?)null) { }

    public SafeHttpClient(string baseUri) : this(new Uri($"{baseUri.TrimEnd('/')}/")) { }

    SafeHttpClient(Uri? baseUri)
    {
        _client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = ConnectCallback,
            MaxAutomaticRedirections = 5,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            UseProxy = false,
        })
        {
            BaseAddress = baseUri,
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public Task<HttpResponseMessage> GetAsync(string? requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        return _client.GetAsync(requestUri, completionOption, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    #region SSRF
    static readonly IPNetwork[] RestrictedIPv4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
    ];

    static bool IsRestricted(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            foreach (var network in RestrictedIPv4Networks)
                if (network.Contains(address))
                    return true;
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                address.IsIPv6UniqueLocal ||
                address.IsIPv6Multicast)
                return true;
        }

        return false;
    }

    static async ValueTask<Stream> ConnectCallback(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        if (addresses.Length == 0)
            throw new HttpRequestException($"Failed to resolve '{host}'");

        bool failed = false;
        foreach (var address in addresses)
        {
            if (IsRestricted(address))
                continue;

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                failed = true;
                continue;
            }
        }

        if (failed)
            throw new HttpRequestException($"Failed to connect to '{host}'");

        throw new HttpRequestException($"Host '{host}' is blocked by SSRF guard");
    }
    #endregion
}
