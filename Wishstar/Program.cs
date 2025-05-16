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
WishDatabase.Load(); // Initialize the database

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();

    AppConfig.UseHttps = true;
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
