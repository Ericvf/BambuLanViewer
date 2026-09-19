using BambuLanViewer.Components;
using BambuLanViewer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables();

builder.Services.AddSingleton<IBambuMttqClient, BambuMttqClient>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.MapGet("/preview", async (string t, HttpContext context, ILogger<Program> logger) =>
{
    HttpClient _http = new();

    logger.LogInformation("Fetching preview image from Bambu printer...");
    var url = builder.Configuration.GetSection("BambuMttqClient").GetValue<string>("CameraUrl");
    var bytes = await _http.GetByteArrayAsync($"{url}&t={t}");

    return Results.File(bytes, "image/jpeg");
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();