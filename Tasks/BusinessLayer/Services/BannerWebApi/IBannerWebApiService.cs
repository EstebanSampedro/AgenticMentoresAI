using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.BannerWebApi;

/// <summary>
/// Contrato de interacción con BannerWebApi para operaciones relacionadas
/// con información académica y solicitudes de justificación.
/// </summary>
public interface IBannerWebApiService
{
    /// <summary>
    /// Inicia el flujo de justificación de inasistencia para un estudiante
    /// dentro de BannerWebApi.
    /// </summary>
    /// <param name="request">
    /// Datos de la justificación a crear, incluyendo correo institucional,
    /// fechas involucradas, descripción del evento y documentos adjuntos si aplica.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="StudentJustificationResponse"/> con la información del resultado,
    /// o null si no se pudo completar la operación o si ocurrió un error de comunicación.
    /// </returns>
    Task<StudentJustificationResponse?> StartStudentJustificationFlowAsync(StudentJustificationRequest request);
}
