using System.Security.Cryptography;
using System.Text;

namespace IFCO.WEB.Helpers
{
    public static class EncryptionHelper
    {
        private const string Key = "Sector@2873";

        private const int NonceSize = 12; // 96 bits
        private const int TagSize = 16;   // 128 bits

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] keyBytes = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSize];

            using (var aesGcm = new AesGcm(keyBytes, TagSize))
            {
                aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
            }

            var result = new byte[NonceSize + TagSize + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipherText, 0, result, NonceSize + TagSize, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;

            byte[] fullCipher = Convert.FromBase64String(encryptedText);

            if (fullCipher.Length < NonceSize + TagSize) return null;

            byte[] keyBytes = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] cipherText = new byte[fullCipher.Length - NonceSize - TagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(fullCipher, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(fullCipher, NonceSize + TagSize, cipherText, 0, cipherText.Length);

            byte[] plainBytes = new byte[cipherText.Length];

            using (var aesGcm = new AesGcm(keyBytes, TagSize))
            {
                aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}