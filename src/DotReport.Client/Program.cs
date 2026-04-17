using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using DotReport.Client.Services;
using DotReport.Client.Services.Inference;
using DotReport.Client.Services.Parsers;
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

// Inference backends — registered in priority order (Tier1 → Tier2 → Tier3)
builder.Services.AddScoped<IInferenceBackend, ServerProxyBackend>();
builder.Services.AddScoped<IInferenceBackend, OnnxBackend>();
builder.Services.AddScoped<IInferenceBackend, RuleBasedBackend>();
builder.Services.AddScoped<InferenceCircuitBreaker>();

// Domain services
builder.Services.AddScoped<VRAMDetector>();
builder.Services.AddScoped<IndexedDbService>();
builder.Services.AddScoped<ModelOrchestrator>();
builder.Services.AddScoped<ConsolidatorProxy>();
builder.Services.AddScoped<ReportGenerator>();
builder.Services.AddScoped<DocumentParserFactory>();
builder.Services.AddScoped<SchemaValidator>();
builder.Services.AddScoped<ModelWarmupService>();
builder.Services.AddScoped<CrossDocIntelligenceService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<KnowledgeBase>();

var host = builder.Build();
try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    // Surface any fatal startup exception to the boot screen
    var js = host.Services.GetRequiredService<IJSRuntime>();
    try { await js.InvokeVoidAsync("console.error", $"FATAL: {ex.GetType().Name}: {ex.Message}"); }
    catch { /* JS interop itself failed — exception is already in browser console */ }
}
