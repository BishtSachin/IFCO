using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace IFCO.WEB.Services
{
    // Decrypts the SSO token sent by MyDiary using the bank's central CryptoService
    // (POST {"text": "<encrypted>"} -> {"success": true, "result": "<plain text>", "error": null}).
    // No keys or crypto code live in this application any more.
    public class CryptoServiceClient
    {
        private const string DefaultDecryptUrl = "https://app2.unionbankofindia.co.in/CryptoService/api/Crypto/decrypt";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CryptoServiceClient> _logger;
        private readonly string _decryptUrl;

        public CryptoServiceClient(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings, ILogger<CryptoServiceClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            // Optional override in appsettings (ApiSettings:CryptoDecryptUrl); defaults to the central service
            _decryptUrl = string.IsNullOrWhiteSpace(apiSettings.Value.CryptoDecryptUrl)
                ? DefaultDecryptUrl
                : apiSettings.Value.CryptoDecryptUrl;
        }

        // Returns the plain text, or null when the token cannot be decrypted for any reason.
        // The token and the decrypted value are never written to the log.
        public async Task<string?> DecryptAsync(string cipherText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cipherText)) return null;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                using var response = await client.PostAsJsonAsync(_decryptUrl, new CryptoRequest(cipherText), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CryptoService decrypt returned HTTP {Status}", (int)response.StatusCode);
                    return null;
                }

                var body = await response.Content.ReadFromJsonAsync<CryptoResponse>(cancellationToken: cancellationToken);
                if (body == null || !body.Success || string.IsNullOrEmpty(body.Result))
                {
                    _logger.LogWarning("CryptoService decrypt was not successful: {Error}", body?.Error);
                    return null;
                }

                return body.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CryptoService decrypt call failed");
                return null;
            }
        }

        private record CryptoRequest(string Text);

        private class CryptoResponse
        {
            public bool Success { get; set; }
            public string? Result { get; set; }
            public string? Error { get; set; }
        }
    }
}
