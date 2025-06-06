using Microsoft.Extensions.DependencyInjection;

namespace SpawnDev.BlazorJS.TransformersJS.DepthAnythingV2
{
    /// <summary>
    /// Add extensions for DepthAnythingService to IServiceCollection.
    /// </summary>
    public static class IServiceCollectionExtension
    {
        /// <summary>
        /// Adds the DepthAnythingService to the service collection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDepthAnything(this IServiceCollection services)
        {
            return services.AddSingleton<DepthAnythingService>();
        }
        /// <summary>
        /// Adds the DepthAnythingService to the service collection with a configuration action.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static IServiceCollection AddDepthAnything(this IServiceCollection services, Action<DepthAnythingService> config)
        {
            return services.AddSingleton<DepthAnythingService>(p =>
            {
                var depthAnythingService = ActivatorUtilities.CreateInstance<DepthAnythingService>(p);
                config(depthAnythingService);
                return depthAnythingService;
            });
        }
    }
}
