namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Options;

public sealed class AiBatchingOptions
{
    public int WindowSeconds { get; set; } = 30;
    public int ScanIntervalSeconds { get; set; } = 3;
    public int MaxBatchesPerScan { get; set; } = 50;
    public int MaxParallelBatches { get; set; } = 4;
}
