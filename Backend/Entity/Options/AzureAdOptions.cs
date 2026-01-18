namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;

public class AzureAdOptions
{
    public string Instance { get; set; }
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string WebhookClientState { get; set; }
    public string NotificationUrl { get; set; }    
}
