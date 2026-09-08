using IFCO.WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace IFCO.WEB.Controllers
{
    public class OtpSessionState
    {
        public string Otp { get; set; }
        public DateTime Expiry { get; set; }
        public int ResendAttempts { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly OracleService _dbService;
        private readonly ApiSettings _apiSettings;
        private readonly RsaKeyService _rsaKeyService;
        private readonly ApiClientService _apiClientService;
        private readonly IMemoryCache _cache; // --- ADD THIS ---
        private const int MaxLoginAttempts = 5; // Allow 5 login attempts...
        private readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10); // ...within a 10-minute window.
        private readonly MyDiaryCryptoService _myDiaryCryptoService;
        private readonly ILogger<AuthController> _logger;
        public AuthController(OracleService dbService, IOptions<ApiSettings> apiSettings, RsaKeyService rsaKeyService, ApiClientService apiClientService, IMemoryCache memoryCache, MyDiaryCryptoService myDiaryCryptoService, ILogger<AuthController> logger)
        {
            _dbService = dbService;
            _apiSettings = apiSettings.Value;
            _rsaKeyService = rsaKeyService;
            _apiClientService = apiClientService;
            _cache = memoryCache;
            _myDiaryCryptoService = myDiaryCryptoService;
            _logger = logger;
        }

        [HttpGet("public-key")]
        public IActionResult GetPublicKey()
        {
            return Ok(new { publicKey = _rsaKeyService.GetPublicKey() });
        }
        public class LoginViewModel
        {
            [Required]
            public string LoginType { get; set; }

            [Required(ErrorMessage = "User ID is required")]
            [StringLength(100, ErrorMessage = "User ID cannot exceed 100 characters")]
            [RegularExpression(@"^[a-zA-Z0-9@._-]*$", ErrorMessage = "User ID contains invalid characters")]
            public string UserId { get; set; }

            public string? P { get; set; }

            public string? MobileNumber { get; set; }

            public string CaptchaQuestion { get; set; }

            [Required]
            [RegularExpression(@"^[0-9]+$", ErrorMessage = "Captcha answer must be a number")]
            public string UserCaptchaAnswer { get; set; }
        }
        public class OtpViewModel
        {
            [Required]
            [RegularExpression(@"^[a-zA-Z0-9@._-]*$", ErrorMessage = "User ID contains invalid characters")]
            public string UserId { get; set; }

            [Required]
            [StringLength(6, MinimumLength = 6)]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits")]
            public string UserOtp { get; set; }
        }
        public class ResendOtpViewModel
        {
            [Required]
            [RegularExpression(@"^[a-zA-Z0-9@._-]*$", ErrorMessage = "User ID contains invalid characters")]
            public string UserId { get; set; }
        }

        // --- UPDATED LOGIN METHOD WITH BYPASSES ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model)
        {
            // 1. Check Login Attempts (Keep existing logic)
            if (model.LoginType == "Consultant")
            {
                var cacheKey = $"LoginAttempts_{model.UserId}";
                _cache.TryGetValue(cacheKey, out int attemptCount);
                if (attemptCount >= MaxLoginAttempts) return LocalRedirect($"~/consultant?status=AccountLocked");

                // Only increment attempts if we are NOT bypassing, or increment anyway? 
                // Usually good to keep it to prevent spamming even in bypass mode.
                attemptCount++;
                _cache.Set(cacheKey, attemptCount, LockoutDuration);
            }

            // 2. Captcha (You can also bypass this if you want, but usually we keep it)
            if (!ValidateCaptcha(model.CaptchaQuestion, model.UserCaptchaAnswer))
            {
                return LocalRedirect($"~/consultant?status=InvalidCaptcha");
            }

            // --- SCENARIO A: CONSULTANT LOGIN ---
            if (model.LoginType == "Consultant")
            {
                string decryptedMobileNumber = _rsaKeyService.Decrypt(model.MobileNumber);
                if (string.IsNullOrEmpty(decryptedMobileNumber) || !System.Text.RegularExpressions.Regex.IsMatch(decryptedMobileNumber,@"^\d{10}$")) 
                    return LocalRedirect($"~/consultant?status=InvalidCredentials");

                var consultant = await _dbService.GetConsultantByLoginIdAsync(model.UserId);

                // Basic validation that user exists and mobile matches
                if (consultant == null || !consultant.MobileNumber.EndsWith(decryptedMobileNumber))
                {
                    return LocalRedirect($"~/consultant?status=InvalidCredentials");
                }
                if (consultant.Status == "Inactive") return LocalRedirect($"~/consultant?status=AccountInactive");

                // --- CHECK BYPASS FLAG ---
                if (_apiSettings.EnableAuthBypass)
                {
                    // BYPASS MODE: Login directly without OTP
                    return await SignInAndRedirect(consultant.ConsultantId, consultant.ConsultantType, consultant.ConsultantName, "Consultant");
                }
                else
                {
                    // REAL MODE: Send OTP
                    var (success, otp) = await _apiClientService.SendOtpAsync(consultant.MobileNumber);
                    if (!success) return LocalRedirect($"~/consultant?status=OtpSendFailed");

                    var otpState = new OtpSessionState
                    {
                        Otp = otp,
                        Expiry = DateTime.UtcNow.AddMinutes(1),
                        ResendAttempts = 0
                    };
                    HttpContext.Session.SetString($"OTP_{model.UserId}", JsonSerializer.Serialize(otpState));
                    return LocalRedirect($"~/consultant?showOtp=true&userId={model.UserId}");
                }
            }
            // --- SCENARIO B: VERTICAL / ADMIN LOGIN ---
            else if (model.LoginType == "VerticalAdmin")
            {
                string decryptedPassword = _rsaKeyService.Decrypt(model.P);
                if (string.IsNullOrEmpty(decryptedPassword)) return LocalRedirect($"~/?status=InvalidCredentials");

                //_apiSettings.EnableAuthBypass = true;
                // --- CHECK BYPASS FLAG ---
                if (!_apiSettings.EnableAuthBypass)
                {
                    // REAL MODE: Validate with Active Directory
                    var adUserDetails = await _apiClientService.ValidateAdUserAsync(model.UserId, decryptedPassword);
                    if (adUserDetails == null || adUserDetails.ValidationStatus != "true")
                    {
                        return LocalRedirect($"~/?status=AdAuthFailed");
                    }
                }
                // IF BYPASS is true, we skip the block above and just check if user exists in DB below.

                // Check Local DB Authorization
                var user = await _dbService.GetVerticalUserByLoginIdAsync(model.UserId);
                if (user == null) return LocalRedirect($"~/?status=UnauthorizedUser");
                if (user.Status == "Inactive") return LocalRedirect($"~/?status=AccountInactive");

                return await SignInAndRedirect(user.UserId, user.UserType, user.UserName, "Vertical", user.VerticalId);
            }

            return LocalRedirect($"~/consultant?status=InvalidCredentials");
        }

        // Helper method to reduce code duplication
        private async Task<IActionResult> SignInAndRedirect(string userId, string role, string displayName, string loginType, string verticalId = "")
        {
            // 1. Generate a new Session Token
            string sessionToken = Guid.NewGuid().ToString();

            // 2. Save this token to the database (Invalidates all previous sessions)
            await _dbService.UpdateSessionTokenAsync(userId, loginType, sessionToken);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userId),
                new Claim(ClaimTypes.Role, role),
                new Claim("DisplayName", displayName),
                new Claim("LoginType", loginType),
                // 3. Add the token to the cookie claims
                new Claim("SessionToken", sessionToken)
            };

            if (!string.IsNullOrEmpty(verticalId))
            {
                claims.Add(new Claim("VerticalId", verticalId));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return LocalRedirect($"~/home");
        }
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromForm] ResendOtpViewModel model)
        {
            var sessionKey = $"OTP_{model.UserId}";
            var otpStateJson = HttpContext.Session.GetString(sessionKey);

            if (string.IsNullOrEmpty(otpStateJson))
            {
                // If there's no session, the user needs to start over.
                return LocalRedirect($"~/consultant?status=InvalidCredentials");
            }

            var otpState = JsonSerializer.Deserialize<OtpSessionState>(otpStateJson);

            if (otpState.ResendAttempts >= 2)
            {
                HttpContext.Session.Remove(sessionKey); // Clear session on max attempts
                return LocalRedirect($"~/consultant?showOtp=false&userId={model.UserId}&status=MaxResendAttempts");
            }

            var consultant = await _dbService.GetConsultantByLoginIdAsync(model.UserId);
            if (consultant == null)
            {
                return LocalRedirect($"~/consultant?status=InvalidCredentials");
            }

            var (success, otp) = await _apiClientService.SendOtpAsync(consultant.MobileNumber);
            if (!success)
            {
                return LocalRedirect($"~/consultant?showOtp=true&userId={model.UserId}&status=OtpSendFailed");
            }

            // Update the session state
            otpState.Otp = otp;
            otpState.Expiry = DateTime.UtcNow.AddMinutes(1);
            otpState.ResendAttempts++;
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(otpState));

            // Redirect back to the OTP page
            return LocalRedirect($"~/consultant?showOtp=true&userId={model.UserId}");
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromForm] OtpViewModel model)
        {
            var sessionKey = $"OTP_{model.UserId}";
            var otpStateJson = HttpContext.Session.GetString(sessionKey);

            if (string.IsNullOrEmpty(otpStateJson))
            {
                return LocalRedirect($"~/consultant?showOtp=true&userId={model.UserId}&status=InvalidOtp");
            }

            var otpState = JsonSerializer.Deserialize<OtpSessionState>(otpStateJson);

            // Check if OTP is correct AND not expired
            if (otpState.Otp != model.UserOtp || DateTime.UtcNow > otpState.Expiry)
            {
                return LocalRedirect($"~/consultant?showOtp=true&userId={model.UserId}&status=InvalidOtp");
            }

            // OTP is correct, clear the session state
            HttpContext.Session.Remove(sessionKey);
            _cache.Remove($"LoginAttempts_{model.UserId}");

            var consultant = await _dbService.GetConsultantByLoginIdAsync(model.UserId);
            if (consultant == null)
            {
                return LocalRedirect($"~/consultant?status=InvalidCredentials");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, consultant.ConsultantId),
                new Claim(ClaimTypes.Role, consultant.ConsultantType),
                new Claim("DisplayName", consultant.ConsultantName),
                new Claim("LoginType", "Consultant")
            };
            await SignInUser(claims);
            return LocalRedirect($"~/home");
        }

        private bool ValidateCaptcha(string question, string answer)
        {
            if (string.IsNullOrEmpty(question) || !int.TryParse(answer, out var userAnswer))
                return false;

            var parts = question.Split('+');
            if (parts.Length != 2) return false;

            if (int.TryParse(parts[0].Trim(), out var num1) && int.TryParse(parts[1].Trim(), out var num2))
            {
                return (num1 + num2) == userAnswer;
            }
            return false;
        }

        private async Task SignInUser(List<Claim> claims)
        {
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? status = null)
        {
            // 1. Read the user's LoginType claim BEFORE signing them out
            var loginType = HttpContext.User.FindFirst("LoginType")?.Value;

            // 2. Destroy the secure session cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 3. Determine the correct redirect URL based on their identity
            string returnUrl = loginType == "Consultant" ? "~/consultant" : "~/";

            if (!string.IsNullOrEmpty(status))
            {
                // Use standard URL query parameter formatting
                returnUrl += returnUrl.Contains("?") ? $"&status={status}" : $"?status={status}";
            }

            return LocalRedirect(returnUrl);
        }

        [HttpGet("sso")]
        public async Task<IActionResult> SsoLogin([FromQuery] string data)
        {
            _logger.LogInformation("=================================================");
            _logger.LogInformation("SSO LOGIN ATTEMPT STARTED: {Time}", DateTime.Now);

            if (string.IsNullOrEmpty(data))
            {
                _logger.LogWarning("LOGIN FAILED: No 'data' parameter found in URL.");
                return LocalRedirect("~/?status=InvalidToken");
            }

            _logger.LogInformation("Encrypted Token Received. Length: {Length}", data.Length);
            _logger.LogInformation("Attempting Decryption...");

            string decrypted = _myDiaryCryptoService.Decrypt(data);

            if (string.IsNullOrEmpty(decrypted))
            {
                _logger.LogError("LOGIN FAILED: Decryption returned NULL or EMPTY string.");
                return LocalRedirect("~/?status=InvalidToken");
            }

            _logger.LogInformation("Decryption Successful.");

            // Assuming My Diary sends "UserId|SomeOtherData|TimeString"
            var parts = decrypted.Split('|');
            _logger.LogInformation("Payload Parts Count: {Count}", parts.Length);

            if (parts.Length < 2)
            {
                _logger.LogError("LOGIN FAILED: Invalid Token Format. Expected at least 2 parts, got {Count}", parts.Length);
                return LocalRedirect("~/?status=InvalidToken");
            }

            string userId = parts[0];
            string timeString = parts[^1]; // Gets the last item in the array

            _logger.LogInformation("Parsed Data -> UserID: {UserId}, TimeString: {Time}", userId, timeString);

            // Call the dedicated Timestamp Validation Method
            if (!ValidateLinkTimestamp(timeString))
            {
                // The specific error is already logged inside ValidateLinkTimestamp
                return LocalRedirect("~/?status=TokenExpired");
            }

            _logger.LogInformation("Checking database for UserID: {UserId}...", userId);

            // CHECK PERMISSIONS: Does this user exist in our local IFCO database?
            var user = await _dbService.GetVerticalUserByLoginIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("LOGIN FAILED: User '{UserId}' not found in IFCO Vertical Users table. No Permission.", userId);
                return LocalRedirect("~/?status=NoPermission");
            }

            if (user.Status == "Inactive")
            {
                _logger.LogWarning("LOGIN FAILED: User '{UserId}' exists but account status is 'Inactive'.", userId);
                return LocalRedirect("~/?status=AccountInactive");
            }

            _logger.LogInformation("Validation Passed. Logging in as: {UserName} (Role: {Role}, VerticalId: {VerticalId})", user.UserName, user.UserType, user.VerticalId);
            _logger.LogInformation("Redirecting to Dashboard...");

            // User is validated and authorized. Log them in!
            return await SignInAndRedirect(user.UserId, user.UserType, user.UserName, "Vertical", user.VerticalId);
        }
        private bool ValidateLinkTimestamp(string timeString, int ttlSeconds = 60, int futureSkewSeconds = 30)
        {
            // 1) Try strict ISO 8601 ("o") first.
            if (DateTimeOffset.TryParseExact(
                    timeString,
                    "o",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var tokenTime))
            {
                return ValidateAge(tokenTime, ttlSeconds, futureSkewSeconds);
            }

            // 2) Fallback: accept a small, explicit set of non-ISO formats that you know MyDiary might send.
            // NOTE: Keep this list as SMALL as possible to avoid ambiguity & parsing bugs across cultures.
            // The sample you shared matches "M/d/yyyy h:mm:ss tt" (e.g., 2/23/2026 5:05:08 PM)
            var acceptedFormats = new[]
            {
                "M/d/yyyy h:mm:ss tt",    // 2/3/2026 5:05:08 PM
                "M/d/yyyy hh:mm:ss tt",   // 2/3/2026 05:05:08 PM
                "MM/dd/yyyy h:mm:ss tt",  // 02/03/2026 5:05:08 PM
                "MM/dd/yyyy hh:mm:ss tt", // 02/03/2026 05:05:08 PM
                "d/M/yyyy H:mm:ss",       // 23/2/2026 17:05:08 (24-hour, some locales)
                "dd/MM/yyyy HH:mm:ss"     // 23/02/2026 17:05:08
            };

            // We assume the producer’s clock is LOCAL to them; if there is no offset in the string,
            // treat it as local time and convert to UTC for comparison.
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (DateTime.TryParseExact(
                    timeString,
                    acceptedFormats,
                    culture,
                    System.Globalization.DateTimeStyles.AssumeLocal | System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                    out var localTime))
            {
                var localAsOffset = new DateTimeOffset(localTime, DateTimeOffset.Now.Offset);
                return ValidateAge(localAsOffset, ttlSeconds, futureSkewSeconds);
            }

            _logger.LogError("LOGIN FAILED: Could not parse timestamp '{TimeString}' using ISO or accepted fallbacks.", timeString);
            return false;
        }

        private bool ValidateAge(DateTimeOffset tokenTime, int ttlSeconds, int futureSkewSeconds)
        {
            var now = DateTimeOffset.UtcNow;
            var tokenUtc = tokenTime.ToUniversalTime();
            var age = now - tokenUtc; // positive = token older than now

            _logger.LogInformation(
                "Time Check -> Token UTC: {TokenUtc:o}, Server UTC: {Now:o}, Age (secs): {Age}",
                tokenUtc, now, age.TotalSeconds);

            if (age.TotalSeconds < -futureSkewSeconds)
            {
                _logger.LogWarning("LOGIN FAILED: Invalid Token (From the future). Age was {Age} seconds.", age.TotalSeconds);
                return false;
            }

            if (age.TotalSeconds > ttlSeconds)
            {
                _logger.LogWarning("LOGIN FAILED: Link expired. Age was {Age} seconds.", age.TotalSeconds);
                return false;
            }

            _logger.LogInformation("Timestamp validation passed successfully.");
            return true;
        }
    }
}