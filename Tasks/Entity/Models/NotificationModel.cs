using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class NotificationModel
{
    [JsonProperty("value")]
    public List<NotificationValue> Value { get; set; } = new();
}

public class NotificationValue
{
    [JsonProperty("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [JsonProperty("clientState")]
    public string ClientState { get; set; } = string.Empty;

    [JsonProperty("changeType")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonProperty("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonProperty("resourceData")]
    public ResourceData ResourceData { get; set; } = new();

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("subscriptionExpirationDateTime")]
    public DateTime SubscriptionExpirationDateTime { get; set; }
}

public class ResourceData
{
    [JsonProperty("@odata.type")]
    public string ODataType { get; set; } = string.Empty;

    [JsonProperty("@odata.id")]
    public string ODataId { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}
