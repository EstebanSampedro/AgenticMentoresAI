using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.IdentityModel.Tokens.Jwt;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;

/// <summary>
/// Orquestador responsable de inicializar sesiones de Long-Running OBO (On-Behalf-Of)
/// para usuarios autenticados desde Teams/SSO. 
/// Se encarga de:
/// - Leer el token SSO del usuario.
/// - Extraer claims clave (OID y UPN).
/// - Iniciar el proceso de Long-Running OBO en MSAL.
/// - Almacenar la <c>sessionKey</c> resultante en la tabla de sesiones LRO.
/// </summary>
public sealed class OboSessionOrchestrator : IOboSessionOrchestrator
{
    private readonly IConfidentialClientApplication _confidentialClient;
    private readonly ILroSessionService _lroSessionService;
    private readonly string[] _graphScopes;

    /// <summary>
    /// Inicializa el orquestador de sesiones OBO con el cliente confidencial de MSAL,
    /// el repositorio de sesiones de larga duración y la configuración de scopes de Graph.
    /// </summary>
    /// <param name="confidentialClient">
    /// Instancia de <see cref="IConfidentialClientApplication"/> usada para ejecutar el flujo OBO.
    /// </param>
    /// <param name="lroSessionService">
    /// Servicio encargado de persistir y administrar las sesiones de larga duración (LRO).
    /// </param>
    /// <param name="configuration">
    /// Proveedor de configuración desde el cual se leen los scopes de Graph.
    /// </param>
    public OboSessionOrchestrator(
        IConfidentialClientApplication confidentialClient,
        ILroSessionService lroSessionService,
        IConfiguration configuration)
    {
        _confidentialClient = confidentialClient;
        _lroSessionService = lroSessionService;

        // Se leen los scopes desde configuración, con un valor por defecto razonable
        _graphScopes = (configuration["Graph:Scopes"] ?? "Chat.ReadWrite offline_access")
                  .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Realiza el proceso de bootstrap de una sesión de Long-Running OBO a partir de un
    /// token SSO de usuario proveniente de Teams:
    /// 1) Extrae OID y UPN del token.
    /// 2) Llama a MSAL para iniciar el proceso de larga duración.
    /// 3) Guarda la <c>sessionKey</c> resultante en la tabla de sesiones LRO.
    /// </summary>
    /// <param name="userSsoToken">
    /// Token SSO emitido para el usuario desde Teams / Azure AD. Debe contener al menos el claim <c>oid</c>.
    /// </param>
    /// <param name="createdByHint">
    /// Valor alternativo para identificar al creador de la sesión en caso de no disponer de UPN en el token.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Se lanza si el token SSO está vacío o solo contiene espacios.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si el token no contiene el claim <c>oid</c>.
    /// </exception>
    public async Task BootstrapAsync(string userSsoToken, string createdByHint)
    {
        if (string.IsNullOrWhiteSpace(userSsoToken))
            throw new ArgumentException("El token SSO del usuario es requerido.", nameof(userSsoToken));

        // Extrae OID/UPN
        var jwtHandler = new JwtSecurityTokenHandler();
        var jwt = jwtHandler.ReadJwtToken(userSsoToken);

        // OID es obligatorio para poder asociar la sesión LRO a un usuario concreto
        var oid = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
                  ?? throw new InvalidOperationException("El token no contiene el claim 'oid'.");

        // UPN / preferred_username se usa para auditoría y registro
        var upn = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
               ?? jwt.Claims.FirstOrDefault(c => c.Type == "upn")?.Value;

        // MSAL iniciará el proceso de Long-Running OBO, guardará AT/RT en el token cache
        // y devolverá una sessionKey que luego usaremos para AcquireTokenInLongRunningProcess.
        string sessionKey = null!;

        await ((ILongRunningWebApi)_confidentialClient)
            .InitiateLongRunningProcessInWebApi(_graphScopes, userSsoToken, ref sessionKey)
            .ExecuteAsync();

        Console.WriteLine($"[OBO-BOOTSTRAP] Ejecutado para {upn} | sessionKey={sessionKey}");

        // Si el UPN no está disponible, usamos el hint proporcionado por el llamador
        var actor = string.IsNullOrWhiteSpace(upn) ? createdByHint : upn!;

        // Persistir la sessionKey en la tabla de LroSession, desactivando sesiones previas del mismo usuario.
        await _lroSessionService.InsertNewActiveAsync(oid, sessionKey, actor);
    }
}
