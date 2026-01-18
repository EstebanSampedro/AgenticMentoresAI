using System.ComponentModel.DataAnnotations;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public class CreateSubscriptionRequest
{
    [Required]
    public string NotificationUrl { get; set; } = string.Empty;
}
