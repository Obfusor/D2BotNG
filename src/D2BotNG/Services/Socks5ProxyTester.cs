using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using D2BotNG.Core.Protos;

namespace D2BotNG.Services;

/// <summary>
/// Minimal SOCKS5 client used to validate a proxy from the UI "Test" button.
/// Opens the SOCKS5 tunnel (RFC 1928), authenticates if credentials are supplied
/// (RFC 1929), then issues a CONNECT - using a domain address so the proxy resolves
/// DNS remotely - to the Battle.net gateway for the requested realm. It never logs
/// in to Battle.net: reaching the gateway over TCP is enough to prove the proxy works.
/// </summary>
public static class Socks5ProxyTester
{
    public sealed record Result(bool Success, string Message, int LatencyMs);

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const int GatewayPort = 6112;

    public static async Task<Result> TestAsync(string proxy, Realm realm, CancellationToken cancellationToken)
    {
        if (!TryParseProxy(proxy, out var host, out var port, out var user, out var pass, out var parseError))
            return new Result(false, parseError, 0);

        var gateway = GatewayFor(realm);

        var stopwatch = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TestTimeout);
        var ct = cts.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, ct);
            var stream = client.GetStream();

            var hasAuth = !string.IsNullOrEmpty(user);

            // Greeting: version 5, then the auth methods we support.
            await stream.WriteAsync(
                hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 }, ct);

            var method = new byte[2];
            await stream.ReadExactlyAsync(method, ct);
            if (method[0] != 0x05)
                return new Result(false, "Not a SOCKS5 proxy (unexpected version byte)", 0);

            switch (method[1])
            {
                case 0x00:
                    break; // no authentication required
                case 0x02:
                    if (!hasAuth)
                        return new Result(false, "Proxy requires a username and password", 0);
                    var authError = await AuthenticateAsync(stream, user!, pass ?? "", ct);
                    if (authError != null)
                        return new Result(false, authError, 0);
                    break;
                case 0xFF:
                    return new Result(false, hasAuth
                        ? "Proxy rejected username/password authentication"
                        : "Proxy requires authentication but no credentials were given", 0);
                default:
                    return new Result(false, $"Proxy chose an unsupported auth method (0x{method[1]:X2})", 0);
            }

            // CONNECT request using a domain address so the proxy performs DNS resolution.
            var domain = Encoding.ASCII.GetBytes(gateway);
            var request = new byte[7 + domain.Length];
            request[0] = 0x05; // version
            request[1] = 0x01; // CONNECT
            request[2] = 0x00; // reserved
            request[3] = 0x03; // address type: domain name
            request[4] = (byte)domain.Length;
            domain.CopyTo(request, 5);
            request[5 + domain.Length] = GatewayPort >> 8;
            request[6 + domain.Length] = GatewayPort & 0xFF;
            await stream.WriteAsync(request, ct);

            // Reply: VER REP RSV ATYP BND.ADDR BND.PORT
            var reply = new byte[4];
            await stream.ReadExactlyAsync(reply, ct);
            if (reply[0] != 0x05)
                return new Result(false, "Malformed SOCKS5 reply from proxy", 0);
            if (reply[1] != 0x00)
                return new Result(false, $"Proxy could not reach {gateway}:{GatewayPort} ({ReplyText(reply[1])})", 0);

            // Consume the bound address/port so the protocol is left in a clean state.
            await DrainBoundAddressAsync(stream, reply[3], ct);

            stopwatch.Stop();
            return new Result(true,
                $"Reached {gateway}:{GatewayPort} through the proxy", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Result(false, $"Timed out after {TestTimeout.TotalSeconds:0}s", 0);
        }
        catch (Exception ex)
        {
            return new Result(false, $"Connection failed: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// RFC 1929 username/password sub-negotiation.
    /// Returns null on success, otherwise an error message.
    /// </summary>
    private static async Task<string?> AuthenticateAsync(NetworkStream stream, string user, string pass, CancellationToken ct)
    {
        var u = Encoding.UTF8.GetBytes(user);
        var p = Encoding.UTF8.GetBytes(pass);
        if (u.Length > 255 || p.Length > 255)
            return "Proxy username or password is too long (max 255 bytes)";

        var auth = new byte[3 + u.Length + p.Length];
        auth[0] = 0x01; // sub-negotiation version
        auth[1] = (byte)u.Length;
        u.CopyTo(auth, 2);
        auth[2 + u.Length] = (byte)p.Length;
        p.CopyTo(auth, 3 + u.Length);
        await stream.WriteAsync(auth, ct);

        var response = new byte[2];
        await stream.ReadExactlyAsync(response, ct);
        return response[1] == 0x00 ? null : "Proxy authentication failed (bad username or password)";
    }

    private static async Task DrainBoundAddressAsync(NetworkStream stream, byte addressType, CancellationToken ct)
    {
        int length = addressType switch
        {
            0x01 => 4,   // IPv4
            0x04 => 16,  // IPv6
            0x03 => -1,  // domain name (length-prefixed)
            _ => 0
        };

        if (length == -1)
        {
            var lengthByte = new byte[1];
            await stream.ReadExactlyAsync(lengthByte, ct);
            length = lengthByte[0];
        }

        if (length > 0)
            await stream.ReadExactlyAsync(new byte[length + 2], ct); // address bytes + 2-byte port
    }

    /// <summary>
    /// Parses socks5://[user:pass@]host:port using System.Uri. Returns false with a
    /// human-readable error if the value is not a valid SOCKS5 proxy URI.
    /// </summary>
    private static bool TryParseProxy(
        string proxy, out string host, out int port, out string? user, out string? pass, out string error)
    {
        host = "";
        port = 0;
        user = null;
        pass = null;
        error = "";

        if (string.IsNullOrWhiteSpace(proxy))
        {
            error = "Proxy address is required";
            return false;
        }

        if (!Uri.TryCreate(proxy.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Invalid proxy. Expected socks5://[user:pass@]host:port";
            return false;
        }

        if (!uri.Scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unsupported proxy scheme '{uri.Scheme}'. Use socks5://[user:pass@]host:port";
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            error = "Proxy host is required";
            return false;
        }

        if (uri.Port < 0)
        {
            error = "Proxy port is required (socks5://host:port)";
            return false;
        }

        if (uri.Port is < 1 or > 65535)
        {
            error = "Invalid proxy port (must be 1-65535)";
            return false;
        }

        host = uri.DnsSafeHost;
        port = uri.Port;

        var userInfo = uri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var separator = userInfo.IndexOf(':');
            if (separator >= 0)
            {
                user = Uri.UnescapeDataString(userInfo[..separator]);
                pass = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
            }
            else
            {
                user = Uri.UnescapeDataString(userInfo);
                pass = "";
            }
        }

        return true;
    }

    private static string GatewayFor(Realm realm) => realm switch
    {
        Realm.UsWest => "uswest.battle.net",
        Realm.UsEast => "useast.battle.net",
        Realm.Europe => "europe.battle.net",
        Realm.Asia => "asia.battle.net",
        _ => "useast.battle.net"
    };

    private static string ReplyText(byte code) => code switch
    {
        0x01 => "general SOCKS server failure",
        0x02 => "connection not allowed by ruleset",
        0x03 => "network unreachable",
        0x04 => "host unreachable",
        0x05 => "connection refused",
        0x06 => "TTL expired",
        0x07 => "command not supported",
        0x08 => "address type not supported",
        _ => $"reply code 0x{code:X2}"
    };
}
