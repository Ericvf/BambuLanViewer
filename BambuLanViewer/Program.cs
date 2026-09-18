using BambuLanViewer.Components;
using BambuLanViewer.Services;

var builder = WebApplication.CreateBuilder(args);

// Register MQTT service
builder.Services.AddSingleton<IBambuMttqClient, BambuMttqClient>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
