namespace Academikus.AnalysisMentoresVerdes.Proxy.Extensions;

using System.ClientModel;
using Azure.AI.OpenAI;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ServiceCollectionProxyExtensions
{
    public static IServiceCollection AddProxy(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AzureOpenAIOptions>()
            .Bind(config.GetSection("AzureOpenAI"))
            .Validate(o =>
                !string.IsNullOrWhiteSpace(o.Endpoint) &&
                !string.IsNullOrWhiteSpace(o.Key) &&
                !string.IsNullOrWhiteSpace(o.Deployment),
                "Config AzureOpenAI incompleta (Endpoint/Key/Deployment).")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            return new AzureOpenAIClient(new Uri(o.Endpoint), new ApiKeyCredential(o.Key));
        });

        services.AddScoped<IGenerativeClient, AzureOpenAiClient>();
        return services;
    }
}
