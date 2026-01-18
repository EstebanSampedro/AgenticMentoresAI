using Academikus.AnalysisMentoresVerdes.Business.Abstractions;
using Academikus.AnalysisMentoresVerdes.Business.Services;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academikus.AnalysisMentoresVerdes.Business.Extensions;

public static class ServiceCollectionBusiness
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddOptions<AnalysisOptions>().Bind(cfg.GetSection("Analysis"));
        services.AddScoped<IWeeklyAnalysisService, WeeklyAnalysisService>();
        return services;
    }
}
