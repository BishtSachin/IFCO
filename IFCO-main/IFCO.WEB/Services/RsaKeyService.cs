using System.Security.Cryptography;
using System.Text;

namespace IFCO.WEB.Services
{
    public interface IRsaKeyService
    {
        string GetPublicKey();
        string Decrypt(string encryptedText);
    }

    public class RsaKeyService : IRsaKeyService
    {
        private readonly RSA _rsa;

        public RsaKeyService()
        {
            _rsa = RSA.Create(2048);
        }

        public string GetPublicKey()
        {
            var publicKey = _rsa.ExportSubjectPublicKeyInfo();
            var base64Key = Convert.ToBase64String(publicKey);
            return $"-----BEGIN PUBLIC KEY-----\n{ChunkString(base64Key, 64)}\n-----END PUBLIC KEY-----";
        }

        public string Decrypt(string encryptedText)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedText)) return null;

                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = _rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decryption Failed: {ex.Message}");
                return null;
            }
        }

        private string ChunkString(string str, int maxChunkSize)
        {
            return string.Join("\n", Enumerable.Range(0, (int)Math.Ceiling((double)str.Length / maxChunkSize))
               .Select(i => str.Substring(i * maxChunkSize, Math.Min(maxChunkSize, str.Length - i * maxChunkSize))));
        }
    }
}