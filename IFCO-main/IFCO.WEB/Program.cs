using IFCO.WEB.Components;
using IFCO.WEB.Services;
using Serilog;
using Microsoft.AspNetCore.Authentication.Cookies;

// This basic Serilog configuration is good to keep for logging.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Hour,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    //builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
    var apiSettingsSection = builder.Configuration.GetSection("ApiSettings");
    var apiSettings = apiSettingsSection.Get<ApiSettings>();
    //var secretString = apiSettingsSection.GetValue<string>("AdApiClientSecret");
    //if (!string.IsNullOrEmpty(secretString))
    //{
    //    apiSettings.AdApiClientCode = secretString.ToCharArray();
    //}
    // --- START: ADD AUTHENTICATION SERVICES ---
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "IFCO.AuthCookie";
            options.LoginPath = "/";
            options.AccessDeniedPath = "/access-denied";
            options.LogoutPath = "/api/auth/logout";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
        });

    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();
    // --- END: ADD AUTHENTICATION SERVICES ---

    builder.Services.AddControllers();

    builder.Services.AddDistributedMemoryCache(); // Adds a default in-memory implementation of IDistributedCache
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(5); // How long the session can be idle before it's abandoned
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents()
        .AddHubOptions(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

    // We keep the services needed for our other pages.
    builder.Services.AddDataProtection();
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<OracleService>();
    builder.Services.AddScoped<ApiClientService>();
    builder.Services.AddSingleton<RsaKeyService>();
    builder.Services.AddSingleton<MyDiaryCryptoService>();
    builder.Services.Configure<ApiSettings>(apiSettingsSection);

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    // --- START: ADD AUTHENTICATION MIDDLEWARE ---
    // IMPORTANT: Add these lines in this specific order
    app.UseAuthentication();
    app.UseAuthorization();
    // --- END: ADD AUTHENTICATION MIDDLEWARE ---
    app.UseSession();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Add this line to map your new controller
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}