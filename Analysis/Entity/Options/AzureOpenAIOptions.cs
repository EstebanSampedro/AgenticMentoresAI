namespace Academikus.AnalysisMentoresVerdes.Entity.Options;

public sealed class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
}
