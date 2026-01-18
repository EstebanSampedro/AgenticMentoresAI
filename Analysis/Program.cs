using Academikus.AnalysisMentoresVerdes.Business.Extensions;
using Academikus.AnalysisMentoresVerdes.Data.Extensions;
using Academikus.AnalysisMentoresVerdes.Proxy.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;
using System.Threading.Tasks;
using System;

var builder = Host.CreateApplicationBuilder(args);

// Registra capas
builder.Services.AddBusiness(builder.Configuration);
builder.Services.AddData(builder.Configuration);
builder.Services.AddProxy(builder.Configuration);

// Quartz: Job semanal (cron configurable en appsettings)
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("WeeklyAnalysis");
    q.AddJob<WeeklyAnalysisJob>(opts => opts.WithIdentity(jobKey));

    var cron = builder.Configuration["Quartz:WeeklyCron"] ?? "0 30 23 ? * SUN"; // domingo 23:30
    q.AddTrigger(t => t
        .ForJob(jobKey)
        .WithIdentity("WeeklyAnalysis-trigger")
        .WithCronSchedule(cron));
});

// Host de Quartz
builder.Services.AddQuartzHostedService(opt =>
{
    opt.WaitForJobsToComplete = true;
});

var app = builder.Build();
await app.RunAsync();

/// <summary>Job que invoca el servicio de análisis semanal.</summary>
public sealed class WeeklyAnalysisJob : IJob
{
    private readonly Academikus.AnalysisMentoresVerdes.Business.Abstractions.IWeeklyAnalysisService _svc;
    private readonly Microsoft.Extensions.Logging.ILogger<WeeklyAnalysisJob> _log;

    public WeeklyAnalysisJob(
        Academikus.AnalysisMentoresVerdes.Business.Abstractions.IWeeklyAnalysisService svc,
        Microsoft.Extensions.Logging.ILogger<WeeklyAnalysisJob> log)
        => (_svc, _log) = (svc, log);

    public async Task Execute(IJobExecutionContext context)
    {
        _log.LogInformation("WeeklyAnalysisJob triggered by Quartz.");
        try
        {
            var runId = await _svc.RunAsync(window: null, dryRun: false, ct: context.CancellationToken);
            _log.LogInformation("WeeklyAnalysisJob finished. RunId={RunId}", runId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WeeklyAnalysisJob failed.");
            throw; // deja que Quartz registre el fallo
        }
    }
}
