using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsServerIdentity
    {
        private const string CertificateFriendlyName = "ColorVision Operations HTTPS";
        private readonly string _identityDirectory;

        public OperationsServerIdentity(string? identityDirectory = null)
        {
            _identityDirectory = identityDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Operations");
            HostId = LoadOrCreateHostId();
            Certificate = LoadOrCreateCertificate();
            CertificateSha256 = Convert.ToHexString(SHA256.HashData(Certificate.RawData)).ToLowerInvariant();
        }

        public string HostId { get; }

        public X509Certificate2 Certificate { get; }

        public string CertificateSha256 { get; }

        private string LoadOrCreateHostId()
        {
            Directory.CreateDirectory(_identityDirectory);
            string path = Path.Combine(_identityDirectory, "host-id");
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path).Trim();
                if (Guid.TryParseExact(existing, "N", out _))
                    return existing;
            }

            string hostId = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, hostId, new UTF8Encoding(false));
            return hostId;
        }

        private X509Certificate2 LoadOrCreateCertificate()
        {
            string subject = $"CN=ColorVision Operations {HostId}";
            using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            X509Certificate2? existing = store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, subject, validOnly: false)
                .OfType<X509Certificate2>()
                .Where(item => item.HasPrivateKey && item.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30))
                .OrderByDescending(item => item.NotAfter)
                .FirstOrDefault();
            if (existing != null)
                return existing;

            using RSA rsa = RSA.Create(3072);
            CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            OidCollection usages = new();
            usages.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            san.AddDnsName(Environment.MachineName);
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());

            using X509Certificate2 generated = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
            generated.FriendlyName = CertificateFriendlyName;
            byte[] pfx = generated.Export(X509ContentType.Pfx);
            X509Certificate2 persisted = X509CertificateLoader.LoadPkcs12(
                pfx, null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            persisted.FriendlyName = CertificateFriendlyName;
            store.Add(persisted);
            return persisted;
        }
    }
}
