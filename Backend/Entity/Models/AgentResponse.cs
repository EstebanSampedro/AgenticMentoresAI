namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class AgentResponse
{
    public bool Success { get; set; }
    public int Code { get; set; }
    public string Message { get; set; }
    public AgentResponseData Data { get; set; }
    public object Errors { get; set; }
    public object Meta { get; set; }
}

public class AgentResponseData
{
    public string SessionId { get; set; }
    public string Prompt { get; set; }
    public string Response { get; set; }
    public DateTime Timestamp { get; set; }
}
