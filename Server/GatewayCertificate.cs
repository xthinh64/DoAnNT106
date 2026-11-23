using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace OnlineChessGame.Server;

public sealed class GatewayCertificate : IDisposable
{
    private readonly X509Certificate2Collection certificates = new();
    private readonly X509Certificate2 leaf;
    public SslStreamCertificateContext Context { get; }

    public GatewayCertificate(string path, string password)
    {
        try
        {
            // Load the server key separately so Windows Schannel can use the leaf key handle.
            certificates.Import(path, password, X509KeyStorageFlags.EphemeralKeySet);
            leaf = new X509Certificate2(path, password, X509KeyStorageFlags.UserKeySet);
            if (certificates.Cast<X509Certificate2>().Count(c => c.HasPrivateKey) != 1 || !leaf.HasPrivateKey)
                throw new InvalidOperationException("PFX must contain one server certificate with a private key.");
            if (DateTime.UtcNow < leaf.NotBefore.ToUniversalTime() || DateTime.UtcNow > leaf.NotAfter.ToUniversalTime())
                throw new InvalidOperationException("Certificate is outside its validity period.");
            var intermediates = new X509Certificate2Collection(certificates);
            intermediates.Remove(leaf);
            Context = SslStreamCertificateContext.Create(leaf, intermediates, offline: true);
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        leaf?.Dispose();
        foreach (var certificate in certificates) certificate.Dispose();
    }

    public byte[] ExportLanTrustCertificate()
    {
        var certificate = Context.TargetCertificate;
        if (!certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData))
            throw new InvalidOperationException("PFX này do CA cấp. Hãy dùng tệp .cer của CA gốc cho client LAN.");
        if (certificate.Extensions["2.5.29.17"] is null)
            throw new InvalidOperationException("PFX hiện tại thiếu SAN. Hãy đặt IP LAN và tạo PFX mới trước khi xuất .cer.");
        return certificate.Export(X509ContentType.Cert);
    }
}
