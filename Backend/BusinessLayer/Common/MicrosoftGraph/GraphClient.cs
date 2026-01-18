using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;

public static class GraphClient
{
    public static GraphServiceClient FromAccessToken(string accessToken)
    {
        var tokenProvider = new EphemeralAccessTokenProvider(accessToken);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        var adapter = new HttpClientRequestAdapter(authProvider);

        return new GraphServiceClient(adapter);
    }
}
