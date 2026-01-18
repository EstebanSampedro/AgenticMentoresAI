using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common;

public class MicrosoftGraphAuthService
{
    private readonly GraphOptions _graphOptions;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string _accessToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    public MicrosoftGraphAuthService(IOptions<GraphOptions> graphOptions)
    {
        _graphOptions = graphOptions.Value;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration)
        {
            return _accessToken;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            if (string.IsNullOrWhiteSpace(_graphOptions.TenantId))
                throw new Exception("TenantId is missing in configuration.");

            var app = ConfidentialClientApplicationBuilder
                .Create(_graphOptions.ClientId)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{_graphOptions.TenantId}"))
                .WithClientSecret(_graphOptions.ClientSecret)
                .Build();

            var authResult = await app.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();

            _accessToken = authResult.AccessToken;
            _tokenExpiration = authResult.ExpiresOn.UtcDateTime.AddMinutes(-5); // Restar 5 minutos para renovar antes de la expiración

            return _accessToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
