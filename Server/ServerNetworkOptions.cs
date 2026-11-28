using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace OnlineChessGame.Server;

/// <summary>Shared public gateway bind and development certificate configuration.</summary>
public static class ServerNetworkOptions
{
    /// <summary>
    /// Builds SANs for a development certificate. Wildcard listening addresses are not identities;
    /// use CHESS_CERT_HOSTS for the DNS names or IPs clients actually connect to.
    /// </summary>
    public static SubjectAlternativeNameBuilder CreateSubjectAlternativeNames(IPAddress bindAddress, string? certificateHosts)
    {
        ArgumentNullException.ThrowIfNull(bindAddress);
        var san = new SubjectAlternativeNameBuilder();
        var ips = new HashSet<IPAddress>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIp(IPAddress address)
        {
            if (ips.Add(address)) san.AddIpAddress(address);
        }
        void AddDns(string name)
        {
            if (names.Add(name)) san.AddDnsName(name);
        }

        AddDns("localhost");
        AddIp(IPAddress.Loopback);
        AddIp(IPAddress.IPv6Loopback);
        if (!IsWildcard(bindAddress)) AddIp(bindAddress);

        if (string.IsNullOrWhiteSpace(certificateHosts)) return san;
        foreach (var entry in certificateHosts.Split(','))
        {
            var host = entry.Trim();
            if (IPAddress.TryParse(host, out var ip))
            {
                if (IsWildcard(ip) || (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && ip.ScopeId != 0))
                    throw InvalidCertificateHost();
                AddIp(ip);
            }
            else
            {
                // DNS labels only: no URIs, ports, wildcards, underscores or malformed numeric IPs.
                var dns = host.EndsWith('.') ? host[..^1] : host;
                if (dns.Length is 0 or > 253 || dns.All(c => char.IsAsciiDigit(c) || c == '.') ||
                    dns.Split('.').Any(label => label.Length is 0 or > 63 ||
                        label[0] == '-' || label[^1] == '-' ||
                        label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
                    throw InvalidCertificateHost();
                AddDns(host);
            }
        }
        return san;
    }

    private static bool IsWildcard(IPAddress address) =>
        address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
        (address.IsIPv4MappedToIPv6 && address.MapToIPv4().Equals(IPAddress.Any));

    private static ArgumentException InvalidCertificateHost() => new(
        "CHESS_CERT_HOSTS must contain comma-separated DNS names or concrete IP addresses (not wildcard addresses).",
        "certificateHosts");

    public static IPAddress ResolveBindAddress(string? bindAddress)
    {
        if (string.IsNullOrWhiteSpace(bindAddress)) return IPAddress.Any;
        if (IPAddress.TryParse(bindAddress.Trim(), out var address)) return address;
        throw new ArgumentException("CHESS_BIND_ADDRESS must be an IPv4 or IPv6 address.", nameof(bindAddress));
    }
}
