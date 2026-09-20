using BambuLanViewer.Components;
using BambuLanViewer.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables();

builder.Services
    .AddSingleton<IBambuMttqClient, BambuMttqClient>()
    .AddHttpClient();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

var cameraUrl = builder.Configuration.GetSection("BambuMttqClient").GetValue<string>("CameraUrl");

app.MapGet("/preview", async ([FromServices]HttpClient http, HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation("Fetching preview image from Bambu printer...");

    var bytes = await http.GetByteArrayAsync($"{cameraUrl}");

    return Results.File(bytes, "image/jpeg");
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();