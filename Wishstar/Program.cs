using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Wishstar;
using Wishstar.Components;
using Wishstar.Controllers;
using Wishstar.Services;

// `dotnet Wishstar.dll hash-password <password>` prints a hash for the "Token" field in users.json.
if (args.Length >= 1 && args[0] == "hash-password") {
    string password = args.Length >= 2 ? args[1] : ReadPasswordFromConsole();
    Console.WriteLine(PasswordHasher.Hash(password));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => {
        // Keep disconnected circuits around much longer than the default 3 minutes. Phones and background
        // tabs drop the WebSocket all the time; when the user comes back the circuit can simply be resumed
        // instead of showing "reconnecting" and forcing a full reload.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(30);
        options.DisconnectedCircuitMaxRetained = 200;
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddHubOptions(options => {
        // Browsers throttle timers in background tabs to about once a minute, which delays the client's
        // keep-alive pings. The default 30s timeout then kills the connection although the tab is fine.
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<WishDraftStore>();

// Keys live in the data volume so logins (and antiforgery tokens) survive container restarts.
builder.Services.AddDataProtection()
    .SetApplicationName("Wishstar")
    .PersistKeysToFileSystem(Directory.CreateDirectory(AppConfig.KeysBasePath));

builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Login, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy(RateLimitPolicies.Upload, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

if (builder.Environment.IsDevelopment()) {
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

var app = builder.Build();

AuthTokens.Initialize(app.Services.GetRequiredService<IDataProtectionProvider>());
WishDatabase.Load().Initialize(app.Logger);

string localUrl = Environment.GetEnvironmentVariable("SERVER_URL") ?? "localhost";
if (localUrl.StartsWith("http://")) {
    localUrl = localUrl[7..];
} else if (localUrl.StartsWith("https://")) {
    localUrl = localUrl[8..];
} else if (localUrl.StartsWith('$')) {
    localUrl = "localhost";
}

AppConfig.CurrentDomain = localUrl.TrimEnd('/');

app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    AppConfig.UseHttps = true;
}

app.Use(async (context, next) => {
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    headers.XFrameOptions = "DENY";
    if (!app.Environment.IsDevelopment()) {
        // No inline scripts anywhere; inline styles are needed by Blazor's built-in UI.
        headers.ContentSecurityPolicy =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; " +
            "font-src 'self' data: https://cdnjs.cloudflare.com; " +
            "img-src 'self' data: blob:; " +
            "connect-src 'self'; " +
            "worker-src 'self'; manifest-src 'self'; " +
            "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    }

    await next();
});

// Sliding sessions: re-issue the login cookie on page loads once it is a day old, so active users stay
// logged in while unused sessions still expire after 30 days.
app.Use(async (context, next) => {
    if (HttpMethods.IsGet(context.Request.Method)
        && !context.WebSockets.IsWebSocketRequest
        && context.Request.Cookies.TryGetValue(AppConfig.AuthCookieName, out string? token)
        && AuthTokens.TryValidate(token, out var user, out DateTimeOffset issuedAt)
        && user != null
        && DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromDays(1)) {
        context.Response.Cookies.Append(AppConfig.AuthCookieName, AuthTokens.Create(user), AuthTokens.CreateCookieOptions());
    }

    await next();
});

app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string ReadPasswordFromConsole() {
    Console.Write("Password: ");
    var password = new System.Text.StringBuilder();
    while (true) {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) {
            Console.WriteLine();
            return password.ToString();
        }

        if (key.Key == ConsoleKey.Backspace) {
            if (password.Length > 0) {
                password.Length--;
            }
        } else if (!char.IsControl(key.KeyChar)) {
            password.Append(key.KeyChar);
        }
    }
}
