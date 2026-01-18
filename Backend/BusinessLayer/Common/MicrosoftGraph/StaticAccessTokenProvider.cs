using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;

public sealed class StaticAccessTokenProvider : IAccessTokenProvider
{
    private readonly MicrosoftGraphAuthService _authService;

    public StaticAccessTokenProvider(MicrosoftGraphAuthService authService)
    {
        _authService = authService;
    }

    public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        return await _authService.GetAccessTokenAsync();
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();
}
