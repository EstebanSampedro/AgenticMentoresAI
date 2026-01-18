using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;
using Microsoft.Identity.Client;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Auth;

public sealed class OboTokenService : ITokenService
{
    private readonly IConfidentialClientApplication _confidentialClient;
    private readonly ILroSessionService _lroSessionRepository;

    public OboTokenService(
        IConfidentialClientApplication confidentialClient, 
        ILroSessionService lroSessionRepository)
    { 
        _confidentialClient = confidentialClient; 
        _lroSessionRepository = lroSessionRepository; 
    }

    public async Task<string> AcquireAccessTokenAsync(
        string senderIdOrUpn, string[] scopes, string actorForAudit)
    {
        (int Id, string SessionKeyPlain)? row =
            senderIdOrUpn.Contains("@") ?
                await _lroSessionRepository.GetActiveByEmailAsync(senderIdOrUpn) :
                await _lroSessionRepository.GetActiveByUserAsync(senderIdOrUpn);

        if (row is null) 
            throw new InvalidOperationException("reauth_required");

        try
        {
            var res = await ((ILongRunningWebApi)_confidentialClient)
                .AcquireTokenInLongRunningProcess(scopes, row.Value.SessionKeyPlain)
                .ExecuteAsync();

            await _lroSessionRepository.UpdateLastUsedAsync(row.Value.Id, actorForAudit);
            return res.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            // invalida sesión y pide re-bootstrap
            await _lroSessionRepository.DeactivateAllByUserObjectIdAsync(senderIdOrUpn);
            throw new InvalidOperationException("reauth_required");
        }
    }
}
