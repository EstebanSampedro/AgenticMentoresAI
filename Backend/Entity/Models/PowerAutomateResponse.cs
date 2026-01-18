namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class PowerAutomateResponse<T>
{
    public PowerAutomateData<T> Data { get; set; }
}

public class PowerAutomateData<T>
{
    public List<T> Body { get; set; }
}
