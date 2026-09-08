using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IFCO.WEB.Services
{
    public class MyDiaryCryptoService
    {
        // IMPORTANT: Ensure these keys exactly match the encryption keys used by the My Diary application.
        private readonly byte[] _key = Encoding.UTF8.GetBytes("8GlIlFGLQgyuqTSRCgGt66AP3ZLUZW==");
        private readonly byte[] _iv = Encoding.UTF8.GetBytes("MY3hSP5txFHUYI3=");

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return null;

            try
            {
                // URL-encoded strings often replace '+' with ' ', this reverts it.
                cipherText = cipherText.Replace(" ", "+");
                byte[] encryptedBytes = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(encryptedBytes);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                // In a production environment, you might want to log this exception
                return null;
            }
        }
    }
}