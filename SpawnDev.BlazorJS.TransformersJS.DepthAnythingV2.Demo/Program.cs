using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2;
using SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2.Demo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
// Add SpawnDev.BlazorJSRuntime
builder.Services.AddBlazorJSRuntime();
// Add DepthAnythingService
builder.Services.AddDepthAnything();
// Initialize BlazorJSRuntime and start the application
await builder.Build().BlazorJSRunAsync();
