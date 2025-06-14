using Microsoft.AspNetCore.Components;
using SpawnDev.BlazorJS.JSObjects;
using static System.Net.Mime.MediaTypeNames;

namespace SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2
{
    /// <summary>
    /// This class handles the loading and caching of the Transformer.js DepthAnything V2 depth estimation pipeline.<br/>
    /// It also has methods for creating and caching 2D+Z images from 2D images<br/>
    /// It provides events indicating the progress of loading models
    /// </summary>
    public class DepthAnythingService
    {
        // Files that must be included for offline and browser extension use
        // Transformers.js (version specific)
        // onnx backend runtime (used by depth anything v2)
        // https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.5.2/dist/ort-wasm-simd-threaded.jsep.mjs
        // https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.5.2/dist/ort-wasm-simd-threaded.jsep.wasm
        // DepthAnything v2
        // https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/main/config.json
        // https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/main/preprocessor_config.json
        // fp32 model - default
        // https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/main/onnx/model.onnx
        // fp16 model - only needed if UseFp16IfSupported == true and fp16 is supported by the running web browser
        // https://huggingface.co/onnx-community/depth-anything-v2-small/resolve/main/onnx/model_fp16.onnx
        /// <summary>
        /// The progress percentage from 0 to 100
        /// </summary>
        public float? OverallLoadProgress
        {
            get
            {
                var total = (float)ModelProgresses.Values.Sum(p => p.Total ?? 0);
                if (total == 0f) return null;
                var loaded = (float)ModelProgresses.Values.Sum(p => p.Loaded ?? 0);
                return loaded * 100f / total;
            }
        }
        /// <summary>
        /// True if loading models
        /// </summary>
        public bool Loading { get; private set; }
        /// <summary>
        /// True if loading models
        /// </summary>
        public bool ModelsLoaded => DepthEstimationPipelines.Any();
        /// <summary>
        /// Holds the loading progress for models that are loading
        /// </summary>
        public Dictionary<string, ModelLoadProgress> ModelProgresses { get; } = new();
        /// <summary>
        /// Holds all loaded depth estimation pipelines
        /// </summary>
        public Dictionary<string, DepthEstimationPipeline> DepthEstimationPipelines { get; } = new Dictionary<string, DepthEstimationPipeline>();
        /// <summary>
        /// The default depth estimation model. Used if no model is specified.
        /// </summary>
        public string DefaultDepthEstimationModel { get; set; } = "onnx-community/depth-anything-v2-small";
        /// <summary>
        /// Result from a WebGPU support check
        /// </summary>
        public bool WebGPUSupported { get; private set; }
        /// <summary>
        /// If true, WebGPU will be used if the browser supports it.
        /// </summary>
        public bool UseWebGPU { get; set; } = true;
        /// <summary>
        /// Cache for generated 2DZ images keyed by the image source string
        /// </summary>
        public Dictionary<string, HTMLImageElement> Cached2DZImages { get; } = new Dictionary<string, HTMLImageElement>();
        BlazorJSRuntime JS;

        Transformers? Transformers = null;
        /// <summary>
        /// The location of the models remote files<br/>
        /// Transformers.env.remoteHost
        /// </summary>
        public string RemoteModelsUrl { get; set; }
        /// <summary>
        /// The location of the models remote wasm files<br/>
        /// Transformers.env.backends.onnx.wasm.wasmPaths
        /// </summary>
        public string RemoteWasmsUrl { get; set; }
        /// <summary>
        /// If true, the browser cache will be used to store the models<br/>
        /// This is useful if the models are hosted on a remote server and you want to cache them in the browser.<br/>
        /// This should be disabled if running if a browser extension, as the models are already included in the extension and will be copied to the browser cache for every domain they are used on.<br/>
        /// </summary>
        public bool UseBrowserCache { get; set; } = true;
        /// <summary>
        /// If true, the HuggingFace CDN will be used to load the models and wasm files.<br/>
        /// </summary>
        public bool UseHuggingFaceCDN { get; set; } = false;
        /// <summary>
        /// If true, the fp16 model will be used if the browser supports it.<br/>
        /// </summary>
        public bool UseFp16IfSupported { get; set; } = true;
        bool UseCustomCache = false;
        NavigationManager NavigationManager;
        private Uri _AppBaseUri;
        /// <summary>
        /// Gets or sets the base URI for the application.<br/>
        /// If running in a browser extension, this should be set to the extension's base URI.<br/>
        /// </summary>
        /// <remarks>When setting this property, if the provided URI does not end with a trailing slash, 
        /// a slash will be appended automatically. This ensures consistent behavior when constructing  relative URIs
        /// based on the application base URI.</remarks>
        public Uri AppBaseUri
        {
            get => _AppBaseUri;
            set
            {
                _AppBaseUri = value;
                // ensure the base URI ends with a slash
                if (!_AppBaseUri.ToString().EndsWith("/"))
                {
                    _AppBaseUri = new Uri(_AppBaseUri, "/");
                }
                RCLContentBaseUri = new Uri(AppBaseUri, "_content/SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2/");
                RemoteModelsUrl = GetRCLContentUrl("models/");
                RemoteWasmsUrl = GetRCLContentUrl("backends/onnx/wasm/");
            }
        }
        /// <summary>
        /// Gets the base URI for accessing RCL (Razor Class Library) content.
        /// </summary>
        public Uri RCLContentBaseUri { get; private set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="DepthAnythingService"/> class.
        /// </summary>
        /// <remarks>This constructor initializes key properties of the service, including determining
        /// WebGPU support and setting up base URIs for accessing remote resources.</remarks>
        /// <param name="js">The JavaScript runtime used for interacting with the browser's JavaScript environment.</param>
        /// <param name="navigationManager">The navigation manager used to retrieve the base URI of the application.</param>
        public DepthAnythingService(BlazorJSRuntime js, NavigationManager navigationManager)
        {
            NavigationManager = navigationManager;
            JS = js;
            WebGPUSupported = !JS.IsUndefined("navigator.gpu?.requestAdapter");
            // need to see if Urls are correct in browser extensions
            AppBaseUri = new Uri(navigationManager.BaseUri);
        }
        /// <summary>
        /// Constructs the absolute URL by combining the application's base URI with the specified relative URL.
        /// </summary>
        /// <param name="relativeUrl">The relative URL to append to the application's base URI. This value cannot be null or empty.</param>
        /// <returns>The absolute URL as a string, formed by combining the base URI and the relative URL.</returns>
        public string GetAppUrl(string relativeUrl)
        {
            // this is used to get the URL of the models and wasm files
            // it will be used by Transformers.env.remoteHost and Transformers.env.backends.onnx.wasm.wasmPaths
            return new Uri(AppBaseUri, relativeUrl).ToString();
        }
        /// <summary>
        /// Constructs the absolute URL for a resource within the Razor Class Library (RCL) content directory.
        /// </summary>
        /// <remarks>This method is typically used to resolve the URLs of resources such as models or
        /// WebAssembly files that are hosted within the RCL content directory. The resulting URL can be used to
        /// configure external dependencies or runtime environments.</remarks>
        /// <param name="relativeUrl">The relative URL of the resource within the RCL content directory. This value must not be null or empty.</param>
        /// <returns>A string representing the absolute URL of the specified resource.</returns>
        public string GetRCLContentUrl(string relativeUrl)
        {
            // this is used to get the URL of the models and wasm files
            // it will be used by Transformers.env.remoteHost and Transformers.env.backends.onnx.wasm.wasmPaths
            return new Uri(RCLContentBaseUri, relativeUrl).ToString();
        }
        /// <summary>
        /// Occurs when the state of the object changes.
        /// </summary>
        /// <remarks>Subscribe to this event to be notified whenever the state changes.</remarks>
        public event Action OnStateChange = default!;
        void StateHasChanged()
        {
            OnStateChange?.Invoke();
        }
        void Pipeline_OnProgress(ModelLoadProgress obj)
        {
            if (!string.IsNullOrEmpty(obj.File))
            {
                if (ModelProgresses.TryGetValue(obj.File, out var progress))
                {
                    progress.Status = obj.Status;
                    if (obj.Progress != null) progress.Progress = obj.Progress;
                    if (obj.Total != null) progress.Total = obj.Total;
                    if (obj.Loaded != null) progress.Loaded = obj.Loaded;
                }
                else
                {
                    ModelProgresses[obj.File] = obj;
                }
            }
            StateHasChanged();
        }
        SemaphoreSlim LoadLimiter = new SemaphoreSlim(1);
        /// <summary>
        /// Returns the DepthAnythingV2 depth estimation pipeline, loading it if it is not already loaded.<br/>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<DepthEstimationPipeline> GetDepthEstimationPipeline()
        {
            var useWebGPU = WebGPUSupported && UseWebGPU;
            var model = DefaultDepthEstimationModel;
            var key = $"{DefaultDepthEstimationModel}+{useWebGPU}";
            if (DepthEstimationPipelines.TryGetValue(key, out var depthEstimationPipeline))
            {
                return depthEstimationPipeline;
            }
            await LoadLimiter.WaitAsync();
            try
            {
                if (DepthEstimationPipelines.TryGetValue(key, out depthEstimationPipeline))
                {
                    return depthEstimationPipeline;
                }
                using var OnProgress = new ActionCallback<ModelLoadProgress>(Pipeline_OnProgress);
                var fp16 = await HasWebGpuFp16();
                Loading = true;
                if (Transformers == null)
                {
                    Transformers = await Transformers.Init();
                }
                // Set Transformers environment variables
                // use the models included with the extension
                // https://huggingface.co/docs/transformers.js/api/env#envtransformersenvironment--code-object-code
                if (!UseHuggingFaceCDN)
                {
                    if (!string.IsNullOrEmpty(RemoteModelsUrl))
                    {
                        Transformers.JSRef!.Set("env.remoteHost", RemoteModelsUrl);
                    }
                    // onnx-community/depth-anything-v2-small uses the onnx backend which requires the files below
                    // https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.5.2/dist/ort-wasm-simd-threaded.jsep.mjs
                    // https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.5.2/dist/ort-wasm-simd-threaded.jsep.wasm
                    if (!string.IsNullOrEmpty(RemoteWasmsUrl))
                    {
                        Transformers.JSRef!.Set("env.backends.onnx.wasm.wasmPaths", RemoteWasmsUrl);
                    }
                }
                // whether or not to use the browser cache depends on whether or not this is running in a browser extension<br/>
                Transformers.JSRef!.Set("env.useBrowserCache", UseBrowserCache);
                //Transformers.JSRef!.Set("env.useCustomCache", false);
                //Transformers.JSRef!.Set("env.customCache", new
                //{
                //    match = Callback.Create<Request, CacheMatchOptions?, Task<Response>>(CustomCache_Match),
                //    put = Callback.Create<Request, Response, Task>(CustomCache_Put),
                //});
                // Load Depth Estimation Pipeline
                // if allowing fp16 and not using the cdn, "model_fp16.onnx" will be used instead of "model.onnx" and therefore must be available in the same folder
                depthEstimationPipeline = await Transformers.DepthEstimationPipeline(model, new PipelineOptions
                {
                    Device = useWebGPU ? "webgpu" : null,
                    OnProgress = OnProgress,
                    Dtype = fp16 && UseFp16IfSupported ? "fp16" : "fp32",
                });
                DepthEstimationPipelines[key] = depthEstimationPipeline;
                return depthEstimationPipeline;
            }
            catch (Exception ex)
            {
                JS.Log("Error loading Depth Estimation Pipeline", ex.Message);
                throw new Exception($"Failed to load Depth Estimation Pipeline for model '{model}' with WebGPU={useWebGPU}.", ex);
            }
            finally
            {
                Loading = false;
                ModelProgresses.Clear();
                StateHasChanged();
                LoadLimiter.Release();
            }
        }
        async Task CustomCache_Put(Request request, Response? options)
        {
            string? url = null;
            // check if Request is actually a string
            if (request.JSRef!.TypeOf() == "string")
            {
                url = request.JSRefAs<string>();
            }
            else
            {
                using var requestObj = request.JSRefAs<Request>();
                url = request.Url;
            }
            JS.Log("CustomCache_Put", url);
        }
        async Task<Response?> CustomCache_Match(JSObject request, CacheMatchOptions? options)
        {
            string? url = null;
            if (request.JSRef!.TypeOf() == "string")
            {
                url = request.JSRefAs<string>();
            }
            else
            {
                using var requestObj = request.JSRefAs<Request>();
                url = requestObj.Url;
            }
            JS.Set("_request", url);
            JS.Log("CustomCache_Match", request);
            return null;
        }
        static bool? hasFp16 = null;
        /// <summary>
        /// Returns true if the WebGPU adapter supports the shader-f16 feature, which is required for half-precision floating point (fp16) operations in shaders.
        /// </summary>
        /// <returns></returns>
        async Task<bool> HasWebGpuFp16()
        {
            if (hasFp16 == null)
            {
                try
                {
                    using var navigator = JS.Get<Navigator>("navigator");
                    using var gpu = navigator.Gpu;
                    if (gpu != null)
                    {
                        using var adapter = await gpu.RequestAdapter();
                        using var features = adapter.Features;
                        hasFp16 = features.Has("shader-f16");
                    }
                }
                catch { }
            }
            if (hasFp16 == null) hasFp16 = false;
            return hasFp16.Value;
        }
        SemaphoreSlim ImageTo2DZImageLimiter = new SemaphoreSlim(1);
        public async Task<HTMLImageElement> ImageTo2DZImage(HTMLImageElement image)
        {
            if (image == null || !image.Complete || image.NaturalWidth == 0 || image.NaturalHeight == 0 || string.IsNullOrEmpty(image.Src))
            {
                throw new Exception("Invalid imageUrl");
            }
            var source = image.Src;
            if (Cached2DZImages.TryGetValue(source, out var imageWithDepth))
            {
                return imageWithDepth;
            }
            if (!image.IsImageUsable())
            {
                // try using an image we load ourselves using crossOrigin = "anonymous"
                var altImage = await HTMLImageElement.CreateFromImageAsync(source, "anonymous");
                if (!altImage.IsImageUsable() || altImage.NaturalWidth != image.NaturalWidth || altImage.NaturalHeight != image.NaturalHeight)
                {
#if DEBUG && false
                    JS.Log($"DES: anon failed", image.Src);
#endif
                    altImage = await HTMLImageElement.CreateFromImageAsync(source, "user-credentials");
                    if (!altImage.IsImageUsable() || altImage.NaturalWidth != image.NaturalWidth || altImage.NaturalHeight != image.NaturalHeight)
                    {
#if DEBUG && false
                        JS.Log($"DES: cred failed", image.Src);
#endif
                        throw new Exception("Image cannot be used");
                    }
#if DEBUG && false
                    JS.Log($"DES: cred worked", image.Src);
#endif
                    // successfully loaded image
                    image = altImage;
                }
                else
                {
#if DEBUG && false
                    JS.Log($"DES: anon worked", image.Src);
#endif
                    // successfully loaded image
                    image = altImage;
                }
            }
            try
            {
                await ImageTo2DZImageLimiter.WaitAsync();
                if (Cached2DZImages.TryGetValue(source, out imageWithDepth))
                {
                    return imageWithDepth;
                }
                // get the depth estimation pipeline
                var DepthEstimationPipeline = await GetDepthEstimationPipeline();
                // generate the depth map
                //JS.Log(">> Creating image depthmap");
                // create a RawImage from the HTMLImageElement so the image does not have to be redownloaded
                // this will throw an exception if the image is tainted
                using var rawImage = RawImage.FromImage(image);
                using var depthResult = await DepthEstimationPipeline!.Call(rawImage);
                //JS.Log("<< Depthmap image created");
                using var depthInfo = depthResult.Depth;
                using var depthMapData = depthInfo.Data;
                var depthWidth = depthInfo.Width;
                var depthHeight = depthInfo.Height;
                //Console.WriteLine("Depthmap size: " + depthWidth + "x" + depthHeight);
                // create 2D+Z image object url
                var imageWithDepthObjectUrl = await Create2DZObjectUrl(image, depthMapData, depthWidth, depthHeight);
                imageWithDepth = await HTMLImageElement.CreateFromImageAsync(imageWithDepthObjectUrl);
            }
            catch (Exception ex)
            {
                JS.Log("Depthmap imageUrl creation failed", ex);
            }
            finally
            {
                ImageTo2DZImageLimiter.Release();
            }
            Cached2DZImages[source] = imageWithDepth;
            return imageWithDepth;
        }
        async Task<string> Create2DZObjectUrl(HTMLImageElement rgbImage, Uint8Array grayscale1BPPUint8Array, int width, int height)
        {
            var outWidth = width * 2;
            var outHeight = height;
            var grayscale1BPPBytes = grayscale1BPPUint8Array.ReadBytes();
            var depthmapRGBABytes = Grayscale1BPPToRGBA(grayscale1BPPBytes, width, height);
            using var canvas = new HTMLCanvasElement(outWidth, outHeight);
            using var ctx = canvas.Get2DContext();
            // draw rgb image
            ctx.DrawImage(rgbImage);
            // draw depth map
            ctx.PutImageBytes(depthmapRGBABytes, width, height, width, 0);
            using var blob = await canvas.ToBlobAsync("imageUrl/png");
            var ret = URL.CreateObjectURL(blob);
            return ret;
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(Blob image)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            using var rawImage = await RawImage.FromBlob(image);
            return await DepthEstimationPipeline!.Call(rawImage);
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(OffscreenCanvas image)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            using var rawImage = RawImage.FromCanvas(image);
            return await DepthEstimationPipeline!.Call(rawImage);
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(HTMLCanvasElement image)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            using var rawImage = RawImage.FromCanvas(image);
            return await DepthEstimationPipeline!.Call(rawImage);
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(HTMLImageElement image)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            using var rawImage = RawImage.FromImage(image);
            return await DepthEstimationPipeline!.Call(rawImage);
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(string imageUrl)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            using var rawImage = await RawImage.FromURL(imageUrl);
            return await DepthEstimationPipeline!.Call(rawImage);
        }
        /// <summary>
        /// Generates depth for the specified source
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<DepthEstimationResult?> GenerateDepth(RawImage image)
        {
            var DepthEstimationPipeline = await GetDepthEstimationPipeline();
            return await DepthEstimationPipeline!.Call(image);
        }
        async Task<string> CreateDepthImageObjectUrl(Uint8Array grayscale1BPPUint8Array, int width, int height)
        {
            var grayscale1BPPBytes = grayscale1BPPUint8Array.ReadBytes();
            var depthmapRGBABytes = Grayscale1BPPToRGBA(grayscale1BPPBytes, width, height);
            using var canvas = new HTMLCanvasElement(width, height);
            using var ctx = canvas.Get2DContext();
            ctx.PutImageBytes(depthmapRGBABytes, width, height);
            using var blob = await canvas.ToBlobAsync("imageUrl/png");
            var ret = URL.CreateObjectURL(blob);
            return ret;
        }
        /// <summary>
        /// Converts 1 byte per pixel greyscale image into a 4 bytes per pixel rgba image<br/>
        /// A faster method of doing this should be implemented soon
        /// </summary>
        /// <param name="grayscaleData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public byte[] Grayscale1BPPToRGBA(byte[] grayscaleData, int width, int height)
        {
            var ret = new byte[width * height * 4];
            for (var i = 0; i < grayscaleData.Length; i++)
            {
                var grayValue = grayscaleData[i];
                ret[i * 4] = grayValue;     // Red
                ret[i * 4 + 1] = grayValue; // Green
                ret[i * 4 + 2] = grayValue; // Blue
                ret[i * 4 + 3] = 255;       // Alpha
            }
            return ret;
        }
    }
}
