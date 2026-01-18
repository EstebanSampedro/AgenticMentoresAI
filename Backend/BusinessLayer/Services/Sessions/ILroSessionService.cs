namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;

/// <summary>
/// Define las operaciones necesarias para administrar sesiones de larga duración (LRO)
/// utilizadas dentro del flujo Long-Running OBO de Microsoft Entra ID.
/// Permite crear nuevas sesiones, recuperar sesiones activas, actualizar su uso
/// y desactivar sesiones previamente registradas.
/// </summary>
public interface ILroSessionService
{
    /// <summary>
    /// Registra una nueva sesión activa para un usuario, desactivando previamente
    /// cualquier sesión activa existente asociada al mismo usuario.
    /// </summary>
    /// <param name="userObjectId">
    /// ObjectId del usuario en Microsoft Entra ID. 
    /// Este identificador se utiliza para asociar la sesión con el usuario correspondiente.
    /// </param>
    /// <param name="sessionKeyPlain">
    /// SessionKey sin cifrar generado durante el flujo Long-Running OBO,
    /// el cual será protegido y almacenado de manera persistente.
    /// </param>
    /// <param name="userUpn">
    /// UPN del usuario que ejecuta la operación de registro.
    /// Se utiliza para auditoría en los campos CreatedBy y LastUsedBy.
    /// </param>
    Task InsertNewActiveAsync(string userObjectId, string sessionKeyPlain, string userUpn);

    /// <summary>
    /// Obtiene la sesión activa asociada a un usuario utilizando su ObjectId.
    /// </summary>
    /// <param name="userObjectId">ObjectId del usuario en Entra ID.</param>
    /// <returns>
    /// Una tupla con el identificador interno de la sesión y el sessionKey descifrado,
    /// o <c>null</c> si no existe una sesión activa para el usuario.
    /// </returns>
    Task<(int Id, string SessionKeyPlain)?> GetActiveByUserAsync(string userObjectId);

    /// <summary>
    /// Obtiene la sesión activa asociada a un usuario mediante su correo electrónico o UPN.
    /// </summary>
    /// <param name="emailOrUpn">Correo o UPN del usuario en Entra ID.</param>
    /// <returns>
    /// Una tupla con el identificador interno de la sesión y el sessionKey descifrado,
    /// o <c>null</c> si no existe sesión activa para dicho usuario.
    /// </returns>
    Task<(int Id, string SessionKeyPlain)?> GetActiveByEmailAsync(string emailOrUpn);

    /// <summary>
    /// Actualiza los metadatos de uso de una sesión,
    /// estableciendo la fecha/hora del último acceso y el usuario que realizó la operación.
    /// </summary>
    /// <param name="id">Identificador interno de la sesión LRO.</param>
    /// <param name="userUpn">UPN del usuario responsable del acceso.</param>
    /// <returns>
    /// Una tarea que representa la operación asincrónica.
    /// </returns>
    Task UpdateLastUsedAsync(int id, string userUpn);

    /// <summary>
    /// Desactiva todas las sesiones activas asociadas al usuario identificado por su ObjectId.
    /// </summary>
    /// <param name="userObjectId">ObjectId del usuario cuya sesiones deben desactivarse.</param>
    /// <returns>
    /// Una tarea que representa la operación asincrónica.
    /// </returns>
    Task DeactivateAllByUserObjectIdAsync(string userObjectId);
}
