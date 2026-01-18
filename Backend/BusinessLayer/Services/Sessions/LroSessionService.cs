using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Security;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Microsoft.EntityFrameworkCore;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;

/// <summary>
/// Repositorio encargado de administrar sesiones de larga duración (LRO) utilizadas para el flujo
/// Long-Running OBO de Microsoft Identity. Implementa operaciones para crear, consultar,
/// actualizar y desactivar sesiones persistidas en base de datos.
/// </summary>
public sealed class LroSessionService : ILroSessionService
{
    private readonly DBContext _context;
    private readonly LroCrypto _crypto;

    /// <summary>
    /// Inicializa una nueva instancia del repositorio de sesiones LRO.
    /// </summary>
    /// <param name="context">Contexto de base de datos que contiene la tabla LroSessions.</param>
    /// <param name="crypto">Servicio responsable de cifrar y descifrar sessionKeys.</param>
    public LroSessionService(DBContext context, LroCrypto crypto)
    {
        _context = context;
        _crypto = crypto;
    }

    /// <summary>
    /// Crea una nueva sesión activa de larga duración para un usuario, desactivando cualquier sesión previa.
    /// </summary>
    /// <param name="userObjectId">ObjectId del usuario en Entra ID.</param>
    /// <param name="sessionKeyPlain">SessionKey sin cifrar recibida del flujo OBO.</param>
    /// <param name="userUpn">UPN o identificador del usuario que ejecuta la operación (auditoría).</param>
    /// <remarks>
    /// La operación se ejecuta dentro de una transacción para garantizar consistencia:
    /// primero desactiva sesiones activas y luego registra la nueva sesión protegida.
    /// </remarks>
    public async Task InsertNewActiveAsync(string userObjectId, string sessionKeyPlain, string userUpn)
    {
        // Inicio explícito de transacción para asegurar atomicidad:
        // 1) Desactivar sesiones activas
        // 2) Crear una nueva sesión
        using var tx = await _context.Database.BeginTransactionAsync();

        // Desactivar cualquier sesión activa existente para este usuario
        await _context.LroSessions
            .Where(x => x.UserObjectId == userObjectId && x.IsActive)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.IsActive, false));

        // Registro de la nueva sesión con la sessionKey cifrada
        var now = DateTimeOffset.UtcNow;
        var entity = new LroSession
        {
            UserObjectId = userObjectId,
            UserUpn = userUpn,
            SessionKey = _crypto.Protect(sessionKeyPlain), // cifrado simétrico
            IsActive = true,
            CreatedAt = now,
            CreatedBy = userUpn,
            LastUsedAt = now,
            LastUsedBy = userUpn
        };

        _context.LroSessions.Add(entity);
        await _context.SaveChangesAsync();

        // Confirmación de transacción
        await tx.CommitAsync();
    }

    /// <summary>
    /// Obtiene la sesión LRO activa asociada a un usuario dado su ObjectId.
    /// </summary>
    /// <param name="userObjectId">ObjectId del usuario en Entra ID.</param>
    /// <returns>
    /// Una tupla con el Id interno de la sesión y el sessionKey descifrado,
    /// o <c>null</c> si el usuario no posee una sesión activa.
    /// </returns>
    public async Task<(int Id, string SessionKeyPlain)?> GetActiveByUserAsync(string userObjectId)
    {
        // Selección óptima: solo obtener lo necesario (Id y SessionKey)
        var row = await _context.LroSessions
            .Where(x => x.UserObjectId == userObjectId && x.IsActive)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.SessionKey })
            .FirstOrDefaultAsync();

        if (row is null) 
            return null;

        // Descifrado de la sessionKey almacenada
        return (row.Id, _crypto.Unprotect(row.SessionKey));
    }

    /// <summary>
    /// Obtiene la sesión LRO activa asociada a un usuario dado su correo electrónico o UPN.
    /// </summary>
    /// <param name="emailOrUpn">Correo o UPN utilizado para identificar al usuario.</param>
    /// <returns>
    /// Una tupla con el Id de la sesión y el sessionKey descifrado,
    /// o <c>null</c> si no existe sesión activa asociada al usuario.
    /// </returns>
    public async Task<(int, string)?> GetActiveByEmailAsync(string emailOrUpn)
    {
        var row = await _context.LroSessions
            .Where(x => x.UserUpn == emailOrUpn && x.IsActive)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.SessionKey })
            .FirstOrDefaultAsync();

        if (row is null) 
            return null;

        return (row.Id, _crypto.Unprotect(row.SessionKey));
    }

    /// <summary>
    /// Actualiza el uso reciente de una sesión activa, estableciendo fecha/hora y usuario
    /// que accedió por última vez a la sesión. Utilizado para auditoría y métricas.
    /// </summary>
    /// <param name="id">Id interno de la sesión LRO.</param>
    /// <param name="userUpn">UPN del usuario que realizó la operación.</param>
    /// <returns>Una tarea asincrónica que representa la operación de actualización.</returns>
    public Task UpdateLastUsedAsync(int id, string userUpn)
        => _context.LroSessions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(p => p.LastUsedAt, DateTimeOffset.UtcNow)
                .SetProperty(p => p.LastUsedBy, userUpn));

    /// <summary>
    /// Desactiva todas las sesiones LRO activas asociadas a un usuario mediante su ObjectId.
    /// </summary>
    /// <param name="userObjectId">ObjectId del usuario en Entra ID.</param>
    /// <returns>Una tarea asincrónica que representa la operación de desactivación.</returns>
    public Task DeactivateAllByUserObjectIdAsync(string userObjectId)
        => _context.LroSessions
            .Where(x => x.UserObjectId == userObjectId && x.IsActive)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.IsActive, false));
}
