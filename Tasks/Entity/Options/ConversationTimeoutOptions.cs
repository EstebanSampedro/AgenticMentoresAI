namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Options;

public sealed class ConversationTimeoutOptions
{
    public double StudentInactivityMinutes { get; set; }

    public double CheckIntervalMinutes { get; set; }
}
