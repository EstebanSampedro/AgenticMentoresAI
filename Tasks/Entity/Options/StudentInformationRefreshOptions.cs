namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Options;

public class StudentInformationRefreshOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalDays { get; set; } = 180;         // 6 meses aprox.
    public int CheckIntervalMinutes { get; set; } = 60;  // frec. de verificación
    public int BatchSize { get; set; } = 200;            // opcional
    public int DelayBetweenRequestsMs { get; set; } = 100;
}
