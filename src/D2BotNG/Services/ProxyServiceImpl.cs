using D2BotNG.Core.Protos;
using D2BotNG.Data;
using D2BotNG.Engine;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace D2BotNG.Services;

public class ProxyServiceImpl : ProxyService.ProxyServiceBase
{
    private readonly ProxyRepository _proxyRepository;
    private readonly ProfileRepository _profileRepository;
    private readonly ProfileEngine _profileEngine;

    public ProxyServiceImpl(
        ProxyRepository proxyRepository,
        ProfileRepository profileRepository,
        ProfileEngine profileEngine)
    {
        _proxyRepository = proxyRepository;
        _profileRepository = profileRepository;
        _profileEngine = profileEngine;
    }

    public override async Task<Empty> CreateProxy(Proxy request, ServerCallContext context)
    {
        if (!TryNormalize(request.Address, out var address))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Invalid proxy. Expected socks5://[user:pass@]host:port"));
        }

        if (await _proxyRepository.GetByKeyAsync(address) != null)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, $"Proxy '{address}' already exists"));
        }

        await _proxyRepository.CreateAsync(new Proxy { Address = address });
        await _profileEngine.BroadcastProxiesSnapshotAsync();
        return new Empty();
    }

    public override async Task<Empty> UpdateProxy(UpdateProxyRequest request, ServerCallContext context)
    {
        if (!TryNormalize(request.Proxy.Address, out var newAddress))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Invalid proxy. Expected socks5://[user:pass@]host:port"));
        }

        var oldAddress = request.HasOriginalAddress ? request.OriginalAddress : newAddress;
        if (await _proxyRepository.GetByKeyAsync(oldAddress) == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Proxy '{oldAddress}' not found"));
        }

        if (oldAddress != newAddress)
        {
            if (await _proxyRepository.GetByKeyAsync(newAddress) != null)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, $"Proxy '{newAddress}' already exists"));
            }

            await _proxyRepository.DeleteAsync(oldAddress);
            await _proxyRepository.CreateAsync(new Proxy { Address = newAddress });
            await PropagateProxyChangeAsync(oldAddress, newAddress);
        }

        await _profileEngine.BroadcastProxiesSnapshotAsync();
        return new Empty();
    }

    public override async Task<Empty> DeleteProxy(DeleteProxyRequest request, ServerCallContext context)
    {
        await _proxyRepository.DeleteAsync(request.Address);
        await PropagateProxyChangeAsync(request.Address, null);
        await _profileEngine.BroadcastProxiesSnapshotAsync();
        return new Empty();
    }

    public override async Task<ImportProxiesResponse> ImportProxies(ImportProxiesRequest request, ServerCallContext context)
    {
        var existing = await _proxyRepository.GetAllAsync();
        var addresses = new HashSet<string>(existing.Select(p => p.Address), StringComparer.OrdinalIgnoreCase);

        uint added = 0;
        uint skipped = 0;

        foreach (var rawLine in request.Text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Invalid line, or a duplicate of an address we already have (or saw earlier in this batch).
            if (!TryNormalize(line, out var address) || !addresses.Add(address))
            {
                skipped++;
                continue;
            }

            await _proxyRepository.CreateAsync(new Proxy { Address = address });
            added++;
        }

        if (added > 0)
        {
            await _profileEngine.BroadcastProxiesSnapshotAsync();
        }

        return new ImportProxiesResponse { Added = added, Skipped = skipped };
    }

    public override async Task<TestProxyResponse> TestProxy(TestProxyRequest request, ServerCallContext context)
    {
        // No realm context on the Proxies tab; probe the default (US East) gateway.
        var result = await Socks5ProxyTester.TestAsync(request.Address, Realm.Unspecified, context.CancellationToken);
        return new TestProxyResponse
        {
            Success = result.Success,
            Message = result.Message,
            LatencyMs = result.LatencyMs
        };
    }

    /// <summary>
    /// Keeps profile references in sync when a proxy's address changes (newAddress set)
    /// or the proxy is deleted (newAddress null).
    /// </summary>
    private async Task PropagateProxyChangeAsync(string oldAddress, string? newAddress)
    {
        var profiles = await _profileRepository.GetAllAsync();
        var affected = profiles.Where(p => p.Proxy == oldAddress).ToList();
        foreach (var profile in affected)
        {
            profile.Proxy = newAddress ?? "";
            await _profileRepository.UpdateAsync(profile);

            var instance = _profileEngine.GetInstance(profile.Name);
            if (instance != null && instance.ProxyName == oldAddress)
            {
                instance.ProxyName = newAddress;
            }
        }

        if (affected.Count > 0)
        {
            await _profileEngine.BroadcastProfilesSnapshotAsync();
        }
    }

    /// <summary>
    /// Accepts a socks5://[user:pass@]host:port URI (kept verbatim) or a bare
    /// host:port / host:port:user:pass line (normalized to socks5://).
    /// </summary>
    private static bool TryNormalize(string input, out string address)
    {
        address = "";
        var value = input.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (value.Contains("://"))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                !IsSocksScheme(uri.Scheme) ||
                string.IsNullOrEmpty(uri.Host) ||
                uri.Port is < 1 or > 65535)
            {
                return false;
            }

            address = value;
            return true;
        }

        var parts = value.Split(':');
        if (parts.Length != 2 && parts.Length != 4)
        {
            return false;
        }

        var host = parts[0].Trim();
        if (host.Length == 0 || !int.TryParse(parts[1].Trim(), out var port) || port is < 1 or > 65535)
        {
            return false;
        }

        var credentials = parts.Length == 4 ? $"{parts[2].Trim()}:{parts[3].Trim()}@" : "";
        address = $"socks5://{credentials}{host}:{port}";
        return true;
    }

    private static bool IsSocksScheme(string scheme) =>
        scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase);
}
