using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Cases;

/// <summary>
/// Define las operaciones relacionadas con la gestión de casos en Salesforce.
/// Incluye consultas, obtención de casos recientes y creación de nuevos casos.
/// </summary>
public interface ICaseService
{
    /// <summary>
    /// Obtiene el último caso registrado en Salesforce para un usuario identificado por BannerId.
    /// </summary>
    /// <param name="bannerId">Identificador Banner del usuario.</param>
    /// <returns>
    /// Un objeto <see cref="SalesforceCase"/> con la información del caso más reciente,
    /// o <c>null</c> si no existen casos para el usuario.
    /// </returns>
    Task<SalesforceCase?> GetLastCaseByUserAsync(string bannerId);

    /// <summary>
    /// Obtiene todos los casos relacionados con un usuario identificado por BannerId.
    /// Los casos retornados dependen del SOQL configurado en Salesforce.
    /// </summary>
    /// <param name="bannerId">Identificador Banner del usuario.</param>
    /// <returns>
    /// Una lista de objetos <see cref="SalesforceCase"/>.  
    /// La lista puede estar vacía si no existen casos asociados.
    /// </returns>
    Task<IReadOnlyList<SalesforceCase>> GetCasesByUserAsync(string bannerId);

    /// <summary>
    /// Crea un nuevo caso en Salesforce utilizando información de estudiante, mentor,
    /// y la descripción del caso. El comportamiento depende de la configuración definida
    /// en appsettings (prioridad, estado, cola, owner, etc.).
    /// </summary>
    /// <param name="bannerStudent">BannerId del estudiante asociado al caso.</param>
    /// <param name="bannerMentor">BannerId del mentor responsable del caso.</param>
    /// <param name="ownerEmail">Correo electrónico para asignar el caso a un usuario específico; si se deja vacío, se asigna a una cola.</param>
    /// <param name="subject">Asunto del caso generado o actualizado.</param>
    /// <param name="theme">Tema o categoría del caso (utilizado para determinar metadatos en Salesforce).</param>
    /// <param name="summary">Descripción o resumen del caso.</param>
    /// <param name="nextDate">Fecha programada para el siguiente seguimiento.</param>
    /// <returns>
    /// Un objeto <see cref="CreateCaseResponse"/> indicando si el caso fue creado exitosamente
    /// o si ocurrió un error durante el proceso (configuración, validación o Salesforce).
    /// </returns>
    Task<CreateCaseResponse> CreateCaseAsync(
        string bannerStudent,
        string bannerMentor,
        string ownerEmail,
        string subject,
        string theme,
        string summary,
        DateOnly nextDate);
}
