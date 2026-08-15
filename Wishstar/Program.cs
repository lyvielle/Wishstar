using Microsoft.AspNetCore.HttpOverrides;
using Wishstar;
using Wishstar.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});


if (builder.Environment.IsDevelopment()) {
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

var app = builder.Build();
WishDatabase.Load().Initialize(); // Initialize the database

string localUrl = Environment.GetEnvironmentVariable("SERVER_URL") ?? "localhost";
if (localUrl.StartsWith("http://")) {
    localUrl = localUrl[7..];
} else if (localUrl.StartsWith("https://")) {
    localUrl = localUrl[8..];
}

AppConfig.CurrentDomain = localUrl;

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    AppConfig.UseHttps = true;
}

app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


app.UseStaticFiles();
app.UseAntiforgery();
app.MapControllers();
app.Use(async (context, next) => {
    context.Request.EnableBuffering();
    await next();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();