namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;

/// <summary>
/// Orquestador responsable de inicializar y mantener sesiones Long-Running OBO (On-Behalf-Of),
/// utilizando el token SSO proveniente del tab de Microsoft Teams.
/// 
/// Su función principal es iniciar el flujo OBO de larga duración, poblar el token cache de MSAL 
/// y almacenar el <c>sessionKey</c> asociado al usuario para permitir adquisiciones futuras 
/// de Access Tokens sin requerir nuevo consentimiento.
/// </summary>
public interface IOboSessionOrchestrator
{
    /// <summary>
    /// Inicializa una sesión Long-Running OBO a partir del token SSO recibido desde Microsoft Teams,
    /// siembra el MSAL token cache y persiste el <c>sessionKey</c> generado para futuras operaciones.
    /// </summary>
    /// <param name="userSsoToken">
    /// Token SSO emitido por Microsoft Teams usado para bootstrapear el flujo OBO.
    /// Debe contener al menos los claims <c>oid</c> y <c>preferred_username</c>.
    /// </param>
    /// <param name="createdByHint">
    /// Texto opcional para registrar quién ejecuta la operación en caso de que 
    /// el token SSO no contenga un UPN válido. Se utiliza como fallback para auditoría.
    /// </param>
    /// <returns>
    /// Una tarea asincrónica que completa cuando el bootstrap OBO ha sido finalizado
    /// y el <c>sessionKey</c> ha sido persistido en la base de datos.
    /// </returns>
    Task BootstrapAsync(string userSsoToken, string createdByHint);
}
