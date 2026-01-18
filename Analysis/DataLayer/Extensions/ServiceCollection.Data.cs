using Academikus.AnalysisMentoresVerdes.Data.Repositories;
using Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academikus.AnalysisMentoresVerdes.Data.Extensions;

public static class ServiceCollectionData
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration _)
    {
        services.AddScoped<IMessageQueryRepository, MessageQueryRepository>();
        services.AddScoped<IAnalysisWriteRepository, AnalysisWriteRepository>();
        return services;
    }
}
