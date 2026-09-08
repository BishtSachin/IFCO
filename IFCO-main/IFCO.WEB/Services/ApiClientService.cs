using IFCO.WEB.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiSettings _apiSettings;

    public ApiClientService(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings)
    {
        _httpClientFactory = httpClientFactory;
        _apiSettings = apiSettings.Value;
    }

    public async Task<UserDetails?> ValidateAdUserAsync(string userId, string password)
    {
        char[] passwordArray = password.ToCharArray();
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        var client = new HttpClient(handler);

        var apiUrl = _apiSettings.AdValidationUrl + "/validateDomainUser";
        var ci = _apiSettings.AdApiClientId;
        var cs = _apiSettings.AdApiClientCode;

        var authString = $"{ci}:{cs}";
        var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));

        var request = new UserRequest(userId, new String(passwordArray));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
        //client.DefaultRequestHeaders.Authorization = GenerateBasicAuthHeader();

        var response = await client.PostAsJsonAsync(apiUrl, request);

        Array.Clear(passwordArray, 0, passwordArray.Length);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UserDetails>();
        }
        else
        {
            // Log the error for debugging
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"API Error: {errorContent}");
            return null;
        }
    }

    public async Task<(bool Success, string Otp)> SendOtpAsync(string mobileNumber)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = _apiSettings.OtpUrl;

        var random = new Random();
        var otp = random.Next(100000, 999999).ToString();

        var message = $"Dear user, Your authentication code for IFCO-FR web application is {otp}. Union Bank Of India.";

        var fullUrl = $"{baseUrl}{mobileNumber}&message={Uri.EscapeDataString(message)}";

        Console.WriteLine($"Generated OTP for {mobileNumber}: {otp}");

        try
        {
            var response = await client.GetAsync(fullUrl);
            return (response.IsSuccessStatusCode, otp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending OTP: {ex.Message}");
            return (false, string.Empty);
        }
    }
    private AuthenticationHeaderValue GenerateBasicAuthHeader()
    {
        byte[] idBytes = Encoding.ASCII.GetBytes(_apiSettings.AdApiClientId);

        byte[] sepBytes = Encoding.ASCII.GetBytes(":");

        byte[] secretBytes = Encoding.ASCII.GetBytes(_apiSettings.AdApiClientCode);

        byte[] combined = new byte[idBytes.Length + sepBytes.Length + secretBytes.Length];
        Buffer.BlockCopy(idBytes, 0, combined, 0, idBytes.Length);
        Buffer.BlockCopy(sepBytes, 0, combined, idBytes.Length, sepBytes.Length);
        Buffer.BlockCopy(secretBytes, 0, combined, idBytes.Length + sepBytes.Length, secretBytes.Length);

        string base64Auth = Convert.ToBase64String(combined);

        Array.Clear(secretBytes, 0, secretBytes.Length);
        Array.Clear(combined, 0, combined.Length);

        return new AuthenticationHeaderValue("Basic", base64Auth);
    }
}