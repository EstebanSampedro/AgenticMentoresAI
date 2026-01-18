using Azure.Security.KeyVault.Secrets;
using Azure;
using System.Text.Json;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services;

public class KeyVaultTokenStoreService : ITokenStoreService
{
    private readonly SecretClient _secretClient;

    public KeyVaultTokenStoreService(SecretClient secretClient)
    {
        _secretClient = secretClient;
    }

    public async Task StoreTokenAsync(string key, string token)
    {
        try
        {
            // Console.WriteLine($"Se guarda el token {token} con clave {key}");

            await _secretClient.SetSecretAsync(key, token);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error al crear el token: {ex}");
        }
        
    }

    public async Task<string?> GetTokenAsync(string key)
    {
        try
        {
            var secret = await _secretClient.GetSecretAsync(key);

            // Console.WriteLine($"Se obtiene el token {secret} de la clave {key}");

            return secret.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine($"Error al obtener el token: {key}. {ex}");

            return null;
        }
    }

    public async Task<string?> GetCachedTokenAsync(string cacheKey)
    {
        try
        {
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(cacheKey);
            var tokenInfo = JsonSerializer.Deserialize<TokenCacheEntry>(secret.Value);

            if (tokenInfo != null && DateTime.UtcNow < tokenInfo.ExpiresOn)
                return tokenInfo.AccessToken;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            // Token no encontrado
        }

        return null;
    }

    public async Task SaveTokenAsync(string cacheKey, string accessToken, DateTime expiresOn)
    {
        var tokenInfo = new TokenCacheEntry
        {
            AccessToken = accessToken,
            ExpiresOn = expiresOn
        };

        var json = JsonSerializer.Serialize(tokenInfo);
        await _secretClient.SetSecretAsync(cacheKey, json);
    }

    private class TokenCacheEntry
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresOn { get; set; }
    }
}
