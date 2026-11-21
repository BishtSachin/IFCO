namespace IFCO.WEB.Services
{
    public class ApiSettings
    {
        public string AdValidationUrl { get; set; }
        public string OtpUrl { get; set; }
        public string AdApiClientId { get; set; }
        public string AdApiClientCode { get; set; }
        public bool EnableAuthBypass { get; set; }
        public string MyDiaryUrl { get; set; }
        // Optional. Central CryptoService decrypt endpoint; a built-in default is used when empty.
        public string? CryptoDecryptUrl { get; set; }
    }
}