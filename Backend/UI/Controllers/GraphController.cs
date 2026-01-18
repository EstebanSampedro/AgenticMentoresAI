using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

public class GraphController : ControllerBase
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ITokenStoreService _tokenStore;

    public GraphController(ITokenAcquisition tokenAcquisition, ITokenStoreService tokenStore)
    {
        _tokenAcquisition = tokenAcquisition;
        _tokenStore = tokenStore;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cacheKey = "GraphAccessToken";

        var token = await _tokenStore.GetCachedTokenAsync(cacheKey);

        if (token == null)
        {
            token = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] {
                "https://graph.microsoft.com/.default"
            });

            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            var expiresOn = jwt.ValidTo;

            await _tokenStore.SaveTokenAsync(cacheKey, token, expiresOn);
        }

        return Ok(new { token });
    }
}
