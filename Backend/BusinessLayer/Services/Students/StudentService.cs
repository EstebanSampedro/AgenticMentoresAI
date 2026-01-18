using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;

public class StudentService : IStudentService
{
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly DBContext _context;

    public StudentService(
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        DBContext context)
    {
        _serviceAccount = serviceAccountOptions.Value;
        _context = context;
    }

    /// <summary>
    /// Obtiene la información contextual del estudiante a partir de su correo electrónico.
    /// </summary>
    /// <param name="email">Correo institucional o personal asociado al estudiante.</param>
    /// <returns>
    /// Un objeto <see cref="StudentContextDto"/> con los datos del estudiante, o null si no existe.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el correo proporcionado no es válido.
    /// </exception>
    public async Task<StudentContextDto?> GetStudentContextByEmailAsync(string email)
    {
        // Se valida que el correo sea válido antes de ejecutar la consulta
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo del estudiante es obligatorio.", nameof(email));

        // Normalización del correo para evitar problemas de coincidencia por mayúsculas o espacios
        var normalizedEmail = email.Trim().ToLower();

        return await (

            from student in _context.UserTables.AsNoTracking()

            // Buscar al estudiante por email
            where student.Email.ToLower() == normalizedEmail

            // Unir con Chat para obtener el mentor activo
            join chat in _context.Chats.AsNoTracking()
                on student.Id equals chat.StudentId
                into chatJoin
            from chat in chatJoin.DefaultIfEmpty()

            // Unir con UserTable nuevamente para obtener datos del mentor
            join mentor in _context.UserTables.AsNoTracking()
                on chat.MentorId equals mentor.Id
                into mentorJoin
            from mentor in mentorJoin.DefaultIfEmpty()

            select new StudentContextDto
            {
                Identification = student.Identification,
                BannerId = student.BannerId,
                FullName = student.FullName,
                FavoriteName = student.FavoriteName,
                Gender = student.Gender,
                Career = student.Career,
                Faculty = student.Faculty,
                CurrentSemester = student.CurrentSemester ?? 0,
                MentorGender = mentor.Gender
            }
        )
        .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Actualiza los campos relacionados con Banner (BannerId, Pidm, Identification, Career) 
    /// para un estudiante identificado por su correo electrónico.
    /// </summary>
    /// <param name="studentEmail">Correo del estudiante cuyos datos serán actualizados.</param>
    /// <param name="bannerId">Nuevo BannerId.</param>
    /// <param name="pidm">Nuevo PIDM.</param>
    /// <param name="identification">Nueva identificación personal.</param>
    /// <param name="career">Nueva carrera asociada al estudiante.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el correo proporcionado no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task UpdateStudentBannerFieldsAsync(
        string studentEmail,
        string? bannerId,
        string? pidm,
        string? identification,
        string? career)
    {
        // Se valida que el correo sea válido y se normaliza para asegurar coincidencias consistentes
        if (string.IsNullOrWhiteSpace(studentEmail))
            throw new ArgumentException("El correo del estudiante es obligatorio.", nameof(studentEmail));

        var normalizedEmail = studentEmail.Trim().ToLower();

        try
        {
            // Se busca al usuario correspondiente en la base de datos
            var user = await _context.UserTables
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            // Si no existe, se termina la ejecución sin generar error
            if (user is null)
                return;

            bool needsUpdate = false;

            // Se comparan los valores actuales con los nuevos
            if (user.BannerId != bannerId)
            {
                user.BannerId = bannerId;
                needsUpdate = true;
            }

            if (user.Pidm != pidm)
            {
                user.Pidm = pidm;
                needsUpdate = true;
            }

            if (user.Identification != identification)
            {
                user.Identification = identification;
                needsUpdate = true;
            }

            if (user.Career != career)
            {
                user.Career = career;
                needsUpdate = true;
            }

            // Si hubo cambios, se aplican los metadatos de auditoría
            if (needsUpdate)
            {
                user.UpdatedBy = _serviceAccount.Email;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura cualquier error que ocurra al guardar los cambios
            Console.WriteLine($"Error actualizando campos Banner para email={studentEmail}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Crea un nuevo registro de estudiante en la base de datos a partir de los datos proporcionados por Excel.
    /// </summary>
    /// <param name="dto">Modelo con la información del estudiante proveniente del archivo Excel.</param>
    /// <returns>
    /// Identificador interno generado para el estudiante recién creado.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando el modelo proporcionado es nulo.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el modelo no contiene información esencial como el correo del estudiante.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar la información en la base de datos.
    /// </exception>
    public async Task<int> CreateStudentAsync(ExcelStudentModel dto)
    {
        // Se valida que el objeto recibido contenga información válida
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "El modelo del estudiante no puede ser nulo.");

        if (string.IsNullOrWhiteSpace(dto.StudentEmail))
            throw new ArgumentException("El correo del estudiante es obligatorio.", nameof(dto.StudentEmail));

        // Se normaliza el correo electrónico del estudiante para evitar inconsistencias
        var normalizedEmail = dto.StudentEmail.Trim().ToLower();

        try
        {
            // Se crea una nueva entidad UserTable a partir de los datos recibidos desde Excel
            var newStudent = new UserTable
            {
                Email = normalizedEmail,
                FullName = dto.FullName,
                FavoriteName = dto.PreferredName,
                UserRole = "Estudiante",
                UserType = dto.Type,
                UserState = dto.Status,
                SpecialConsideration = dto.SpecialConsideration,
                Gender = dto.Gender,
                CreatedBy = _serviceAccount.Email,
                UpdatedBy = _serviceAccount.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserTables.Add(newStudent);

            // Se guarda el registro en la base de datos y se retorna el identificador generado
            await _context.SaveChangesAsync();
            return newStudent.Id;
        }
        catch (Exception ex)
        {
            // Se captura cualquier error para evitar detener el proceso de sincronización
            Console.WriteLine($"Error creando estudiante desde Excel (email={dto.StudentEmail}). Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza la información de un estudiante utilizando los datos provenientes de Excel,
    /// solo si se detectan cambios respecto a los valores actuales.
    /// </summary>
    /// <param name="id">Identificador interno del estudiante.</param>
    /// <param name="dto">Datos actualizados del estudiante obtenidos desde Excel.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador es inválido o los datos no contienen información esencial.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al actualizar la información en la base de datos.
    /// </exception>
    public async Task UpdateStudentAsync(int id, ExcelStudentModel dto)
    {
        // Se valida que el identificador y los datos del estudiante sean correctos
        if (id <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(id));

        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "El modelo del estudiante no puede ser nulo.");

        try
        {
            // Se obtiene el registro existente en la base de datos
            var student = await _context.UserTables.FindAsync(id);
            if (student == null)
                return;

            bool needsUpdate = false;

            // Se compara cada campo para determinar si existe algún cambio real
            if (student.FullName != dto.FullName)
            {
                student.FullName = dto.FullName;
                needsUpdate = true;
            }

            if (student.FavoriteName != dto.PreferredName)
            {
                student.FavoriteName = dto.PreferredName;
                needsUpdate = true;
            }

            if (student.UserState != dto.Status)
            {
                student.UserState = dto.Status;
                needsUpdate = true;
            }

            if (student.UserType != dto.Type)
            {
                student.UserType = dto.Type;
                needsUpdate = true;
            }

            if (student.SpecialConsideration != dto.SpecialConsideration)
            {
                student.SpecialConsideration = dto.SpecialConsideration;
                needsUpdate = true;
            }

            if (student.Gender != dto.Gender)
            {
                student.Gender = dto.Gender;
                needsUpdate = true;
            }

            // Si hubo modificaciones, se actualizan los metadatos y se guardan los cambios
            if (needsUpdate)
            {
                student.UpdatedBy = _serviceAccount.Email;
                student.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se registra el error únicamente si ocurre una excepción
            Console.WriteLine($"Error actualizando estudiante Id={id}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Desactiva a un estudiante estableciendo su estado a 'Inactivo'.
    /// </summary>
    /// <param name="studentId">Identificador interno del estudiante.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador proporcionado no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task DeactivateStudentAsync(int studentId)
    {
        // Se valida que el identificador del estudiante sea válido antes de ejecutar la operación
        if (studentId <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(studentId));

        try
        {
            // Se busca al estudiante correspondiente en la base de datos
            var student = await _context.UserTables
                .FirstOrDefaultAsync(u => u.Id == studentId && u.UserRole == "Estudiante");

            // Si existe y no se encuentra ya inactivo, se actualiza su estado y auditoría
            if (student != null && student.UserState != "Inactivo")
            {
                student.UserState = "Inactivo";
                student.UpdatedAt = DateTime.UtcNow;
                student.UpdatedBy = _serviceAccount.Email;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura cualquier error ocurrido durante la desactivación
            Console.WriteLine($"Error desactivando estudiante Id={studentId}. Detalle: {ex}");
            throw;
        }
    }
}
