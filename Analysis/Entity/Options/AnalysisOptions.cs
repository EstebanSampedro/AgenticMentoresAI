namespace Academikus.AnalysisMentoresVerdes.Entity.Options;

/// <summary>Parámetros de ejecución del análisis semanal.</summary>
public sealed class AnalysisOptions
{
    /// <summary>Zona horaria para calcular la semana (IANA o Windows). Ej: "America/Bogota".</summary>
    public string Timezone { get; set; } = "America/Bogota";

    /// <summary>Etiqueta del modelo usado (para auditoría en AnalysisRun).</summary>
    public string? ModelVersion { get; set; } = "gpt-4o-mini";

    /// <summary>Etiqueta de versión del prompt.</summary>
    public string? PromptVersion { get; set; } = "v1";

    /// <summary>Grado de paralelismo máximo al llamar a la IA.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 3;

    /// <summary>Límite de turnos por transcripción antes de trocear (si en el futuro haces chunking).</summary>
    public int ChunkMaxTurns { get; set; } = 250;
}
