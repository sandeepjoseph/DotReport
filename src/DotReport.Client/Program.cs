using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DotReport.Client.Services;
using DotReport.Client.Interop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<DotReport.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Core services
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Interop layer
builder.Services.AddScoped<BabylonInterop>();
builder.Services.AddScoped<OnnxInterop>();
builder.Services.AddScoped<IndexedDbInterop>();

// Domain services
builder.Services.AddScoped<VRAMDetector>();
builder.Services.AddScoped<IndexedDbService>();
builder.Services.AddScoped<ModelOrchestrator>();
builder.Services.AddScoped<ConsolidatorProxy>();
builder.Services.AddScoped<ReportGenerator>();
builder.Services.AddSingleton<ThemeService>();

await builder.Build().RunAsync();
