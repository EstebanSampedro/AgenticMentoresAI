namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;

public class SalesforceOptions
{
    public string LoginBaseUrl { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public SalesforceRequestUriOptions RequestUri { get; init; } = new();
    public string GrantType { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string TokenSecret { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";

    public SalesforceQueryOptions Querys { get; init; } = new();
}

public class SalesforceRequestUriOptions
{
    public string Token { get; init; } = "/services/oauth2/token";
    public string ServiceVersion { get; init; } = "services/data/v61.0";
}

public class SalesforceQueryOptions
{
    public string GetMentor { get; init; } = "";
    public string GetStudent { get; init; } = "";
}
