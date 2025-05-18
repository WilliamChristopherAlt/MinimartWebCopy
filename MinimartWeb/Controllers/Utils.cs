using System.Security.Cryptography;

namespace MinimartWeb.Controllers
{
    public static class RsaKeyHelper
    {
        public static (string PublicKeyPem, string PrivateKeyPem) GenerateRsaKeyPair()
        {
            using var rsa = RSA.Create(2048);
            var publicKey = rsa.ExportSubjectPublicKeyInfo();
            var privateKey = rsa.ExportPkcs8PrivateKey();

            return (
                new string(PemEncoding.Write("PUBLIC KEY", publicKey)),
                new string(PemEncoding.Write("PRIVATE KEY", privateKey))
            );
        }
    }


}
