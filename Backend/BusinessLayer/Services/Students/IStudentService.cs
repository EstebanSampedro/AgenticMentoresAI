using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;

/// <summary>
/// Define las operaciones relacionadas con la obtención y administración
/// de información contextual de estudiantes.
/// </summary>
public interface IStudentService
{
    /// <summary>
    /// Obtiene la información contextual del estudiante a partir de su correo electrónico.
    /// </summary>
    /// <param name="email">Correo institucional o personal asociado al estudiante.</param>
    /// <returns>
    /// Un objeto <see cref="StudentContextDto"/> con los datos solicitados, 
    /// o null si no existe un registro vinculado al correo proporcionado.
    /// </returns>
    Task<StudentContextDto?> GetStudentContextByEmailAsync(string email);

    /// <summary>
    /// Actualiza los datos provenientes de BannerWebApi asociados a un estudiante,
    /// como BannerId, Pidm, identificación y carrera.
    /// </summary>
    /// <param name="studentEmail">Correo del estudiante cuyo registro será actualizado.</param>
    /// <param name="bannerId">Identificador Banner del estudiante.</param>
    /// <param name="pidm">PIDM del estudiante.</param>
    /// <param name="identification">Número de identificación del estudiante.</param>
    /// <param name="career">Carrera actual del estudiante.</param>
    Task UpdateStudentBannerFieldsAsync(
        string studentEmail,
        string? bannerId,
        string? pidm,
        string? identification,
        string? career);

    /// <summary>
    /// Crea un nuevo estudiante en la base de datos a partir de los datos provenientes de Excel.
    /// </summary>
    /// <param name="dto">Modelo con los datos del estudiante importados desde Excel.</param>
    /// <returns>
    /// Identificador interno generado para el nuevo estudiante.
    /// </returns>
    Task<int> CreateStudentAsync(ExcelStudentModel dto);

    /// <summary>
    /// Actualiza los datos de un estudiante utilizando la información proporcionada desde Excel.
    /// Solo se modifican los campos cuyo valor haya cambiado.
    /// </summary>
    /// <param name="id">Identificador del estudiante a actualizar.</param>
    /// <param name="dto">Modelo con los valores actualizados de Excel.</param>
    Task UpdateStudentAsync(int id, ExcelStudentModel dto);

    /// <summary>
    /// Desactiva un estudiante cambiando su estado a 'Inactivo'.
    /// </summary>
    /// <param name="studentId">Identificador del estudiante que será desactivado.</param>
    Task DeactivateStudentAsync(int studentId);
}

