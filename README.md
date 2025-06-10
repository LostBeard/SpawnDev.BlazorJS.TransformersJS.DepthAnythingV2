# SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2
[![NuGet version](https://badge.fury.io/nu/SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2.svg?label=SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2)](https://www.nuget.org/packages/SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2)  

Add depth estimation to your Blazor WebAssembly project with [Transformers.js](https://github.com/huggingface/transformers.js/) and [Depth Anything V2 Small](https://huggingface.co/onnx-community/depth-anything-v2-small) for Blazor WebAssembly. Includes fp16 and fp32 models. 

### Demo
[Live Demo](https://lostbeard.github.io/SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2/)

### Getting started

Add the package to your Blazor WebAssembly project using NuGet:
```bash
dotnet add package SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2
```

Example Program.cs 
```cs
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
```

### Usage
Inject the `DepthAnythingService` into your Blazor components or services to use the DepthAnything functionality.
```cs
    [Inject]
    DepthAnythingService DepthAnythingService { get; set; } = default!;
```

```cs
DepthEstimationPipeline pipeline = await DepthAnythingService.GetDepthEstimationPipeline();
...
```