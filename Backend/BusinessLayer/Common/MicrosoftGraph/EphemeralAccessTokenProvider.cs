using Azure.Core;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;

public class EphemeralAccessTokenProvider : IAccessTokenProvider
{
    private readonly string _accessToken;

    public EphemeralAccessTokenProvider(string accessToken)
    {
        _accessToken = accessToken;
    }

    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additional = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    public Task<AccessToken> GetAccessTokenAsync(Uri uri, Dictionary<string, object>? additional = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AccessToken(_accessToken, DateTimeOffset.MaxValue));
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();
}

