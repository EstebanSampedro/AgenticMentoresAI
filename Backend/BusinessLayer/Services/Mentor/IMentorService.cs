using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;

/// <summary>
/// Expone operaciones relacionadas con la gestión de mentores, sincronización desde Excel,
/// integración con Salesforce, recuperación de estudiantes asignados y administración de chat.
/// </summary>
public interface IMentorService
{
    /// <summary>
    /// Obtiene información detallada de un mentor desde Salesforce utilizando su correo electrónico.
    /// </summary>
    /// <param name="email">Correo del mentor a consultar.</param>
    /// <returns>Objeto con la respuesta del API de Salesforce.</returns>
    Task<GeneralSalesforceUserResponse> GetMentorAsync(string email);

    /// <summary>
    /// Obtiene la lista de estudiantes asignados a un mentor filtrando por su correo.
    /// Permite almacenar temporalmente un token externo si se requiere.
    /// </summary>
    /// <param name="mentorEmail">Correo del mentor.</param>
    /// <param name="token">Token opcional para almacenamiento temporal.</param>
    /// <returns>Lista de estudiantes asociados al mentor.</returns>
    Task<List<StudentChatDto>> GetStudentsByMentorEmailAsync(string mentorEmail, string? token);

    /// <summary>
    /// Realiza una búsqueda paginada de mensajes entre un mentor y sus estudiantes.
    /// Permite filtrar por texto del mensaje o nombre del estudiante.
    /// </summary>
    /// <param name="request">Parámetros de búsqueda y paginación.</param>
    /// <returns>Resultado paginado con los mensajes encontrados.</returns>
    Task<PagedResultDto<SearchMessagesDto>> SearchMessagesAsync(SearchMessagesRequest request);

    /// <summary>
    /// Obtiene el tipo de mentor asociado a un correo electrónico.
    /// Permite almacenar temporalmente un token externo si aplica.
    /// </summary>
    /// <param name="email">Correo del mentor.</param>
    /// <param name="token">Token opcional para almacenamiento temporal.</param>
    /// <returns>Objeto con el tipo de mentor o null si no existe o no está activo.</returns>
    Task<MentorTypeDto?> GetMentorTypeByEmailAsync(string email, string token);

    /// <summary>
    /// Obtiene el contenido de un mensaje proactivo según una clave única.
    /// </summary>
    /// <param name="messageKey">Clave que identifica el mensaje.</param>
    /// <returns>Contenido del mensaje o null si no existe.</returns>
    Task<string?> GetMessageByKeyAsync(string messageKey);

    /// <summary>
    /// Obtiene la lista de mentores activos que tienen configurado un correo de respaldo.
    /// </summary>
    /// <param name="state">Estado del mentor filtrado (por ejemplo: Activo).</param>
    /// <returns>Lista de usuarios con rol Mentor y correo de respaldo configurado.</returns>
    Task<List<UserTable>> GetMentorsWithBackupEmailAsync(string state);

    /// <summary>
    /// Recupera un mentor utilizando su correo electrónico.
    /// </summary>
    /// <param name="email">Correo del mentor que se desea consultar.</param>
    /// <returns>El mentor encontrado o null si no existe.</returns>
    Task<UserTable?> GetBackupMentorByEmailAsync(string email);

    /// <summary>
    /// Obtiene el mentor asignado actualmente a un estudiante.
    /// </summary>
    /// <param name="studentId">Identificador del estudiante.</param>
    /// <returns>El mentor asignado o null si no existe relación activa.</returns>
    Task<UserTable?> GetMentorByStudentIdAsync(int studentId);

    /// <summary>
    /// Evalúa una colección de mentores provenientes de Excel y genera un listado
    /// de registros inválidos con su detalle de errores detectados.
    /// </summary>
    /// <param name="mentors">Lista original de mentores obtenidos desde Excel.</param>
    /// <returns>Lista de modelos con descripción de los campos faltantes o inválidos.</returns>
    List<InvalidMentorModel> GetInvalidMentorsDetailed(List<ExcelMentorModel> mentors);

    /// <summary>
    /// Crea un nuevo mentor en la base de datos a partir de los valores obtenidos desde Excel.
    /// </summary>
    /// <param name="dto">Modelo con los datos del mentor provenientes de Excel.</param>
    Task CreateMentorAsync(ExcelMentorModel dto);

    /// <summary>
    /// Activa un mentor cambiando su estado a 'Activo'.
    /// </summary>
    /// <param name="id">Identificador del mentor que se desea activar.</param>
    Task ActivateMentorAsync(int id);

    /// <summary>
    /// Actualiza los datos de un mentor utilizando la información proporcionada desde Excel.
    /// Valida cambios en información general así como actualizaciones de periodos de vacaciones.
    /// </summary>
    /// <param name="id">Identificador interno del mentor.</param>
    /// <param name="dto">Modelo con los datos actualizados del mentor desde Excel.</param>
    Task UpdateMentorAsync(int id, ExcelMentorModel dto);

    /// <summary>
    /// Desactiva un mentor cambiando su estado a 'Inactivo'.
    /// </summary>
    /// <param name="id">Identificador del mentor que será desactivado.</param>
    Task DeactivateMentorAsync(int id);
}
