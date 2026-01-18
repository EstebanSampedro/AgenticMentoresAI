using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common.KeyVault;

public sealed class KeyVaultMappedJsonSecretsSource : IConfigurationSource
{
    private readonly SecretClient _client;
    private readonly IConfigurationSection _mapSection;

    public KeyVaultMappedJsonSecretsSource(SecretClient client, IConfigurationSection mapSection)
    {
        _client = client;
        _mapSection = mapSection;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new KeyVaultMappedJsonSecretsProvider(_client, _mapSection);
}

public sealed class KeyVaultMappedJsonSecretsProvider : ConfigurationProvider
{
    private readonly SecretClient _client;
    private readonly IConfigurationSection _mapSection;

    public KeyVaultMappedJsonSecretsProvider(SecretClient client, IConfigurationSection mapSection)
    {
        _client = client;
        _mapSection = mapSection;
    }

    public override void Load()
    {
        var map = _mapSection.AsEnumerable()
            .Where(kv => kv.Value is not null)
            .Where(kv => kv.Key.StartsWith(_mapSection.Path + ":", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                kv => kv.Key.Substring(_mapSection.Path.Length + 1), // quita "Secrets:Map:"
                kv => kv.Value!,
                StringComparer.OrdinalIgnoreCase);

        foreach (var (logicalName, secretName) in map)
        {
            // Console.WriteLine($"[KV LOAD] key='{logicalName}' secret='{secretName}'");

            string secretValue;
            try
            {
                secretValue = _client.GetSecret(secretName).Value.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                throw new InvalidOperationException(
                    $"KeyVault secret not found. Mapping key='{logicalName}' -> secret='{secretName}'. " +
                    $"Check your Secrets:Map and that the secret exists in the vault.",
                    ex);
            }


            // Si es JSON, lo aplana a logicalName:Prop = Value
            if (LooksLikeJson(secretValue))
            {
                using var doc = JsonDocument.Parse(secretValue);
                FlattenJsonIntoData($"{logicalName}", doc.RootElement);
            }
            else
            {
                // Si no es JSON, lo expone como logicalName (valor directo)
                Data[logicalName] = secretValue;
            }
        }
    }

    private static bool LooksLikeJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        return (s.StartsWith("{") && s.EndsWith("}")) || (s.StartsWith("[") && s.EndsWith("]"));
    }

    private void FlattenJsonIntoData(string prefix, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    FlattenJsonIntoData($"{prefix}:{prop.Name}", prop.Value);
                break;

            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in element.EnumerateArray())
                    FlattenJsonIntoData($"{prefix}:{i++}", item);
                break;

            default:
                Data[prefix] = element.ToString();
                break;
        }
    }
}
