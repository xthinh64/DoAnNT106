using System.Net;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Server;
using Xunit;

namespace TransportTests;

public sealed class ServerNetworkOptionsTests
{
    [Theory]
    [InlineData(null, "0.0.0.0")]
    [InlineData("", "0.0.0.0")]
    [InlineData("  ", "0.0.0.0")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("192.168.1.10", "192.168.1.10")]
    [InlineData("::", "::")]
    [InlineData("::1", "::1")]
    [InlineData(" 2001:db8::1 ", "2001:db8::1")]
    public void ResolvesBindAddress(string? input, string expected)
    {
        Assert.Equal(IPAddress.Parse(expected), ServerNetworkOptions.ResolveBindAddress(input));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("192.168.1.10")]
    [InlineData("2001:db8::1")]
    public void CertificateIncludesLocalIdentitiesAndConcreteBindOnly(string bind)
    {
        var san = ReadSan(bind, null);
        Assert.Contains("localhost", san.EnumerateDnsNames());
        Assert.Contains(IPAddress.Loopback, san.EnumerateIPAddresses());
        Assert.Contains(IPAddress.IPv6Loopback, san.EnumerateIPAddresses());
        var address = IPAddress.Parse(bind);
        if (!address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any))
            Assert.Contains(address, san.EnumerateIPAddresses());
        Assert.DoesNotContain(IPAddress.Any, san.EnumerateIPAddresses());
        Assert.DoesNotContain(IPAddress.IPv6Any, san.EnumerateIPAddresses());
    }

    [Fact]
    public void CertificateHostsAreIndependentOfWildcardBind()
    {
        var san = ReadSan("0.0.0.0", " chess.example.com , 192.168.1.20,2001:db8::2 ");
        Assert.Contains("chess.example.com", san.EnumerateDnsNames());
        Assert.Contains(IPAddress.Parse("192.168.1.20"), san.EnumerateIPAddresses());
        Assert.Contains(IPAddress.Parse("2001:db8::2"), san.EnumerateIPAddresses());
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("*.example.com")]
    [InlineData("https://chess.example.com")]
    [InlineData("chess.example.com:443")]
    [InlineData("bad host")]
    [InlineData("-bad.example")]
    [InlineData("bad_.example")]
    [InlineData("example.com..")]
    [InlineData("::ffff:0.0.0.0")]
    [InlineData("example.com,,localhost")]
    [InlineData("192.168.1.999")]
    [InlineData("fe80::1%3")]
    public void InvalidCertificateHostNamesSetting(string hosts)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ServerNetworkOptions.CreateSubjectAlternativeNames(IPAddress.Any, hosts));
        Assert.Contains("CHESS_CERT_HOSTS", error.Message);
    }

    private static X509SubjectAlternativeNameExtension ReadSan(string bind, string? hosts)
    {
        var extension = ServerNetworkOptions.CreateSubjectAlternativeNames(IPAddress.Parse(bind), hosts).Build();
        return new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("bad-address")]
    [InlineData("127.0.0.1:9000")]
    public void InvalidBindAddressNamesSetting(string input)
    {
        var error = Assert.Throws<ArgumentException>(() => ServerNetworkOptions.ResolveBindAddress(input));
        Assert.Contains("CHESS_BIND_ADDRESS", error.Message);
    }
}
