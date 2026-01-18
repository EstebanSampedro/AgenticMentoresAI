using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Auth;

public interface ITokenService
{
    Task<string> AcquireAccessTokenAsync(
        string senderIdOrUpn, string[] scopes, string actorForAudit);
}
