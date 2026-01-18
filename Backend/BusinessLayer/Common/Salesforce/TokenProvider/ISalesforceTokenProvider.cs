namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;

public interface ISalesforceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}
