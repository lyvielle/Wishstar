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

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();

    AppConfig.UseHttps = true;
} else {
    string localUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "localhost";
    if (localUrl.StartsWith("http://")) {
        localUrl = localUrl[7..];
    } else if (localUrl.StartsWith("https://")) {
        localUrl = localUrl[8..];
    }

    AppConfig.CurrentDomain = localUrl;
}

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
