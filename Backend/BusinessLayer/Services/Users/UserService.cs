using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.BannerWebApi;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Subscriptions;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;

public class UserService : IUserService
{
    private readonly DBContext _context;
    private readonly IMentorService _mentorService;
    private readonly IStudentService _studentService;
    private readonly IChatService _chatService;
    private readonly IMicrosoftGraphService _microsoftGraphService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IBannerWebApiService _bannerWebApiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IConfiguration _configuration;

    public UserService(
        DBContext context,
        IMentorService mentorService,
        IStudentService studentService,
        IChatService chatService,
        IMicrosoftGraphService microsoftGraphService,
        ISubscriptionService subscriptionService,
        IBannerWebApiService bannerWebApiService,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IConfiguration configuration)
    {
        _context = context;
        _mentorService = mentorService;
        _studentService = studentService;
        _chatService = chatService;
        _microsoftGraphService = microsoftGraphService;
        _subscriptionService = subscriptionService;
        _bannerWebApiService = bannerWebApiService;
        _httpClientFactory = httpClientFactory;
        _serviceAccount = serviceAccountOptions.Value;
        _configuration = configuration;
    }

    public async Task SyncUsersFromExcelAsync()
    {
        Console.WriteLine("Iniciando sincronización de usuarios desde Excel");

        var today = DateTime.UtcNow.Date;

        var notificationUrl = _configuration["AzureAd:NotificationUrl"];

        #region Mentores
        // Obtiene los usuarios de Rol Mentor de la BD
        var localMentors = await GetLocalUsersAsync("Mentor");

        // Obtiene el listado de mentores desde el API de Power Automate cuyo
        // origen de datos es un archivo de Excel almacenado en SharePoint
        var excelMentors = await GetMentorsFromExcelAsync();

        // Valida mentores con campos vacíos o nulos
        var invalidMentors = _mentorService.GetInvalidMentorsDetailed(excelMentors);

        // Filtra los mentores válidos
        var validExcelMentors = excelMentors
            .Where(m => !invalidMentors.Any(inv => inv.Email == m.Email))
            .ToList();

        // Obtiene las suscripciones activas
        var activeSubscriptions = await _subscriptionService.ListActiveSubscriptionsAsync();

        foreach (var validExcelMentor in validExcelMentors)
        {
            // Valida si existe un correo en la BD correspondiente al del Excel
            var local = localMentors.FirstOrDefault(x => x.Email == validExcelMentor.Email);

            // Si el mentor no existe en la base de datos local y existe en el Excel,
            // se lo crea en la base de datos, si está activo se crea la suscripción
            if (local == null)
            {
                (string personId, string bannerId, string pidm, List<string> programs) = await 
                    _bannerWebApiService.GetStudentKeyInfoAsync(validExcelMentor.Email);

                validExcelMentor.Identification = personId;
                validExcelMentor.BannerId = bannerId;
                validExcelMentor.Pidm = pidm;

                // Valida la existencia de campos faltantes
                var missingFields = new List<string>();

                if (string.IsNullOrWhiteSpace(bannerId))
                    missingFields.Add("BannerId");

                if (string.IsNullOrWhiteSpace(personId))
                    missingFields.Add("Identification");

                if (string.IsNullOrWhiteSpace(pidm))
                    missingFields.Add("Pidm");

                if (missingFields.Any())
                {
                    // TO DO: Enviar por correo

                    Console.WriteLine($"Mentor {validExcelMentor.Email} creado con advertencia: faltan {string.Join(", ", missingFields)}");
                }

                // Crea el registro en la BD
                await _mentorService.CreateMentorAsync(validExcelMentor);

                // Obtiene información del mentor desde el directorio activo
                var entraUserId = await _microsoftGraphService.GetEntraUserIdByEmailAsync(validExcelMentor.Email);

                if (!string.IsNullOrEmpty(entraUserId))
                {
                    // Actualiza el registro en la base de datos
                    await UpdateUserEntraUserIdAsync(validExcelMentor.Email, entraUserId);
                }
                else if (string.IsNullOrEmpty(entraUserId))
                {
                    invalidMentors.Add(new InvalidMentorModel
                    {
                        Email = validExcelMentor.Email,
                        FullName = validExcelMentor.FullName,
                        MissingFields = "No se encontró EntraUserId en el directorio (Graph)"
                    });

                    continue;
                }

                if (validExcelMentor.Status == "Activo")
                {
                    // Se valida si existe una suscripción activa para el mentor
                    var existingSubscription = activeSubscriptions.FirstOrDefault(sub =>
                            string.Equals(sub.Resource, $"users/{entraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(sub.NotificationUrl, notificationUrl, StringComparison.OrdinalIgnoreCase)
                        );

                    if (existingSubscription == null)
                    {
                        // Se activa una suscripción a los chats del mentor
                        await _subscriptionService.CreateSubscriptionAsync(entraUserId, notificationUrl);
                    }
                    else
                    {
                        Console.WriteLine($"Ya existe una suscripción activa para el usuario {entraUserId}");
                    }
                }
            }
            // Si el mentor existe en la base de datos local y existe en el Excel
            else
            {
                // Se obtiene información del mentor creado desde el directorio activo
                var entraUserId = await _microsoftGraphService.GetEntraUserIdByEmailAsync(validExcelMentor.Email);

                // Si el mentor existe en la base de datos local 
                // se actualizan los datos que hayan cambiado
                await _mentorService.UpdateMentorAsync(local.Id, validExcelMentor);

                if (validExcelMentor.Status == "Activo")
                {
                    await _chatService.UpdateChatsByMentorIdAsync(local.Id, "Activo");

                    // Valida si existe una suscripción activa para el mentor
                    var existingSubscription = activeSubscriptions.FirstOrDefault(sub =>
                            string.Equals(sub.Resource, $"users/{entraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(sub.NotificationUrl, notificationUrl, StringComparison.OrdinalIgnoreCase)
                        );

                    if (existingSubscription == null)
                    {
                        // Activa una suscripción a los chats del mentor
                        await _subscriptionService.CreateSubscriptionAsync(entraUserId, notificationUrl);
                    }
                    else
                    {
                        Console.WriteLine($"Ya existe una suscripción activa para el usuario {entraUserId}");
                    }
                }
                else if (validExcelMentor.Status == "Inactivo")
                {
                    // Desactiva los chats del mentor
                    await _chatService.UpdateChatsByMentorIdAsync(local.Id, "Inactivo");

                    // Valida si existe una suscripción activa para el mentor
                    var existingSubscription = activeSubscriptions.FirstOrDefault(sub =>
                            string.Equals(sub.Resource, $"users/{entraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(sub.NotificationUrl, notificationUrl, StringComparison.OrdinalIgnoreCase)
                        );

                    if (existingSubscription != null)
                    {
                        // Desactiva la suscripción a los chats del mentor
                        await _subscriptionService.DeactivateSubscriptionAsync(existingSubscription.Id);
                    }
                    else
                    {
                        Console.WriteLine($"No existe una suscripción activa para el usuario {entraUserId}");
                    }
                }
                else
                {
                    invalidMentors.Add(new InvalidMentorModel
                    {
                        Email = validExcelMentor.Email,
                        FullName = validExcelMentor.FullName,
                        MissingFields = "Estado inválido (no es 'Activo' ni 'Inactivo')"
                    });
                }
            }
        }

        // Obtiene los correos de los mentores desde el Excel
        var mentorExcelEmails = excelMentors.Select(m => m.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var local in localMentors)
        {
            if (!mentorExcelEmails.Contains(local.Email) && local.UserState == "Activo")
            {
                // Desactiva el mentor en la base de datos
                await _mentorService.DeactivateMentorAsync(local.Id);

                // Desactiva los chats del mentor
                await _chatService.UpdateChatsByMentorIdAsync(local.Id, "Inactivo");

                if (!string.IsNullOrEmpty(local.EntraUserId))
                {
                    var subscriptionToRemove = activeSubscriptions.FirstOrDefault(sub =>
                        string.Equals(sub.Resource, $"users/{local.EntraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase));

                    if (subscriptionToRemove != null)
                    {
                        var success = await _subscriptionService.DeactivateSubscriptionAsync(subscriptionToRemove.Id!);

                        Console.WriteLine(success
                            ? $"Suscripción eliminada para el usuario {local.EntraUserId}"
                            : $"No se pudo eliminar la suscripción para el usuario {local.EntraUserId}");
                    }
                    else
                    {
                        Console.WriteLine($"No se encontró ninguna suscripción activa para el usuario {local.EntraUserId}");
                    }
                }
            }
        }

        // Obtiene los mentores activos que cuentan con un mentor de respaldo
        var activeMentorsWithBackup = await _mentorService.GetMentorsWithBackupEmailAsync("Activo");

        foreach (var activeMentorWithBackup in activeMentorsWithBackup)
        {
            bool isOnVacation = activeMentorWithBackup.UserLeaves.Any(leave =>
                                                                leave.UserLeaveState == "Activo" &&
                                                                today >= leave.StartDate.Date &&
                                                                today <= leave.EndDate.Date
                                                            );

            if (isOnVacation)
            {
                // Desactiva al mentor en la BD
                await _mentorService.DeactivateMentorAsync(activeMentorWithBackup.Id);

                // Desactiva los chats del mentor
                await _chatService.UpdateChatsByMentorIdAsync(activeMentorWithBackup.Id, "Inactivo");

                // Valida si existe una suscripción activa para el mentor
                var existingSubscription = activeSubscriptions.FirstOrDefault(sub =>
                        string.Equals(sub.Resource, $"users/{activeMentorWithBackup.EntraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(sub.NotificationUrl, notificationUrl, StringComparison.OrdinalIgnoreCase)
                    );

                if (existingSubscription != null)
                {
                    // Desactiva la suscripción a los chats del mentor
                    await _subscriptionService.DeactivateSubscriptionAsync(existingSubscription.Id);
                }
                else
                {
                    Console.WriteLine($"No existe una suscripción activa para el usuario {activeMentorWithBackup.EntraUserId}");
                }
            }
        }

        // Obtiene los mentores inactivos que cuentan con un mentor de respaldo
        var inactiveMentorsWithBackup = await _mentorService.GetMentorsWithBackupEmailAsync("Inactivo");

        foreach (var inactiveMentorWithBackup in inactiveMentorsWithBackup)
        {
            // Obtiene el mentor de respaldo del mentor inactivo
            var backupMentor = await _mentorService.GetBackupMentorByEmailAsync(inactiveMentorWithBackup.BackupEmail);

            // Activa al mentor de respaldo en la BD
            await _mentorService.ActivateMentorAsync(backupMentor.Id);

            // Activa los chats del mentor
            await _chatService.UpdateChatsByMentorIdAsync(backupMentor.Id, "Activo");

            // Valida si existe una suscripción activa para el mentor
            var existingSubscription = activeSubscriptions.FirstOrDefault(sub =>
                    string.Equals(sub.Resource, $"users/{backupMentor.EntraUserId}/chats/getAllMessages?model=B", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sub.NotificationUrl, notificationUrl, StringComparison.OrdinalIgnoreCase)
                );

            if (existingSubscription == null)
            {
                // Activa una suscripción a los chats del mentor
                await _subscriptionService.CreateSubscriptionAsync(backupMentor.EntraUserId, notificationUrl);
            }
            else
            {
                Console.WriteLine($"Ya existe una suscripción activa para el usuario {backupMentor.EntraUserId}");
            }
        }

        // Obtiene los usuarios (actualizados) de Rol Mentor de la BD
        var localUpdatedMentors = await GetLocalUsersAsync("Mentor");

        foreach (var localUpdatedMentor in localUpdatedMentors)
        {
            // Agrega o actualiza información del mentor desde BannerWebApi
            // TO DO: actualizar nombre
            await EnrichUserFromBannerWebApiAsync(localUpdatedMentor.Email);
        }
        #endregion

        #region Estudiantes
        // Obtiene los usuarios de Rol Estudiante de la BD
        var localStudents = await GetLocalUsersAsync("Estudiante");

        // Obtiene el listado de estudiantes desde el API de Power Automate cuyo
        // origen de datos es un archivo de Excel almacenado en SharePoint
        var excelStudents = await GetExcelStudentsFromApiAsync();

        // GetInvalidStudentsDetailed???

        foreach (var excelStudent in excelStudents)
        {
            var local = localStudents.FirstOrDefault(x => x.Email == excelStudent.StudentEmail);

            if (local == null && excelStudent.Status == "Activo")
            {
                // Si el estudiante no existe en la base de datos local y
                // está activo en el Excel, se lo crea en la base de datos
                var studentId = await _studentService.CreateStudentAsync(excelStudent);

                // Obtiene información del estudiante creado desde el directorio activo
                var entraUserId = await _microsoftGraphService.GetEntraUserIdByEmailAsync(excelStudent.StudentEmail);

                if (!string.IsNullOrEmpty(entraUserId))
                {
                    // Actualiza el registro en la base de datos
                    await UpdateUserEntraUserIdAsync(excelStudent.StudentEmail, entraUserId);
                }

                // Crea o actualiza el chat asociado al mentor
                await _chatService.CreateOrUpdateChatAsync(studentId, excelStudent.MentorEmail);
            }
            else if (local != null && excelStudent.Status == "Activo")
            {
                // Si el estudiante existe en la base de datos local 
                // se actualizan los datos que hayan cambiado
                await _studentService.UpdateStudentAsync(local.Id, excelStudent);

                // Si se detecta un cambio en el mentor asociado al estudiante
                // en la consulta al Excel, se crea/activa el chat con el mentor
                // nuevo y se desactiva el chat con el mentor antiguo

                // Se crea o actualiza el chat asociado al mentor
                await _chatService.CreateOrUpdateChatAsync(local.Id, excelStudent.MentorEmail);
            }
            else if (local != null && excelStudent.Status == "Inactivo")
            {
                // Si el estudiante existe en la base de datos local 
                // se actualizan los datos que hayan cambiado
                await _studentService.UpdateStudentAsync(local.Id, excelStudent);

                // Si se detecta un cambio en el mentor asociado al estudiante
                // en la consulta al Excel, se crea/activa el chat con el mentor
                // nuevo y se desactiva el chat con el mentor antiguo

                // Se desactiva los chats asociados al estudiante
                await _chatService.DeactivateChatsByStudentIdAsync(local.Id);
            }

            // OBTENER INFORMACIÓN ADICIONAL DEL ESTUDIANTE DESDE BANNERWEBAPI
            await EnrichUserFromBannerWebApiAsync(excelStudent.StudentEmail);
        }

        var studentExcelEmails = excelStudents.Select(m => m.StudentEmail).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var local in localStudents)
        {
            if (!studentExcelEmails.Contains(local.Email))
            {
                // Desactiva cada estudiante que ya no aparece en el Excel
                await _studentService.DeactivateStudentAsync(local.Id);

                // Desactiva el chat asociado a cada 
                // estudiante que ya no aparece en el Excel
                await  _chatService.DeactivateChatsByStudentIdAsync(local.Id);
            }
        }
        #endregion

        // Agrega los chatIds de las conversaciones que tienen estudiantes y mentores
        var localUpdatedStudents = await GetLocalUsersAsync("Estudiante");

        foreach (var student in localUpdatedStudents)
        {
            UserTable mentor = await _mentorService.GetMentorByStudentIdAsync(student.Id);

            // Valida la existencia de mentor e IDs
            if (mentor == null || string.IsNullOrWhiteSpace(mentor.EntraUserId) || string.IsNullOrWhiteSpace(student.EntraUserId))
            {
                continue;
            }

            // Obtiene el chat entre mentor y estudiante
            var chat = await _chatService.GetChatByMentorAndStudentIdAsync(mentor.Id, student.Id);

            // Valida si ya tiene un MSTeamsChatId asignado
            if (chat == null || !string.IsNullOrWhiteSpace(chat.MsteamsChatId))
            {
                continue;
            }

            // Obtiene el chatId desde Microsoft Graph solo si es necesario
            var msTeamsChatId = await _microsoftGraphService.GetOneOnOneChatIdAsync(mentor.EntraUserId, student.EntraUserId);

            if (!string.IsNullOrWhiteSpace(msTeamsChatId))
            {
                chat.MsteamsChatId = msTeamsChatId;
                await _chatService.UpdateChatAsync(chat);
            }
        }

        Console.WriteLine("Sincronización completada exitosamente.");
    }

    /// <summary>
    /// Enriquece la información de un usuario consultando datos clave en Banner WebAPI
    /// y actualizándolos en la base de datos local.
    /// </summary>
    /// <param name="email">Correo asociado al estudiante.</param>
    /// <remarks>
    /// Este método maneja internamente sus excepciones para evitar fallos en flujos dependientes.
    /// </remarks>
    private async Task EnrichUserFromBannerWebApiAsync(string email)
    {
        try
        {
            // Se valida y normaliza el correo recibido para evitar inconsistencias
            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("[BannerEnrichment] Email inválido recibido.");
                return;
            }

            var normalizedEmail = email.Trim().ToLower();

            // Llamada al servicio externo de Banner para obtener la información clave del estudiante
            var (personId, bannerId, pidm, programs) =
                await _bannerWebApiService.GetStudentKeyInfoAsync(normalizedEmail);

            // Se obtiene la carrera desde la lista de programas devuelta por Banner si está disponible
            var career = programs?.FirstOrDefault();

            // Se actualizan los campos relacionados en la base de datos local
            await _studentService.UpdateStudentBannerFieldsAsync(
                normalizedEmail,
                bannerId: bannerId,
                pidm: pidm,
                identification: personId,
                career: career
            );
        }
        catch (Exception ex)
        {
            // Cualquier excepción se captura para evitar que errores externos detengan el flujo principal
            Console.WriteLine($"Error actualizando información desde Banner para {email}. | Error: {ex}");
        }
    }

    /// <summary>
    /// Obtiene la lista de usuarios locales filtrados por el rol especificado.
    /// </summary>
    /// <param name="role">Rol de usuario a filtrar.</param>
    /// <returns>
    /// Lista de entidades <see cref="UserTable"/> cuyo rol coincide con el parámetro proporcionado.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el rol proporcionado no es válido.
    /// </exception>
    private async Task<List<UserTable>> GetLocalUsersAsync(string role)
    {
        // Se valida que el rol sea válido antes de ejecutar la consulta
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("El rol es obligatorio.", nameof(role));

        // Se normaliza el rol recibido para evitar inconsistencias al filtrar
        var normalizedRole = role.Trim();

        // Consulta para obtener usuarios cuyo rol coincide con el solicitado
        return await _context.UserTables
            .AsNoTracking()
            .Where(u => u.UserRole == normalizedRole)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene y normaliza la lista de estudiantes expuesta por el flujo de Power Automate.
    /// </summary>
    /// <returns>
    /// Lista normalizada de objetos <see cref="ExcelStudentModel"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando la URL del flujo no está configurada.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Se produce cuando ocurre un error al consumir el flujo externo.
    /// </exception>
    private async Task<List<ExcelStudentModel>> GetExcelStudentsFromApiAsync()
    {
        // Obtiene el endpoint configurado que expone la información de estudiantes desde Power Automate
        var url = _configuration["ExcelSync:StudentsUrl"];

        // Obtiene la clave requerida por el flujo para autenticación vía header personalizado
        var apiKey = _configuration["ExcelSync:ApiKey"];

        // Se valida la existencia de la URL antes de realizar el consumo del servicio
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("La URL de sincronización de estudiantes no está configurada.");

        // Se valida que la API Key exista ya que es obligatoria para poder consumir el endpoint
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("La API Key para sincronizar estudiantes no está configurada.");

        try
        {
            // Se obtiene una instancia de HttpClient desde el factory para reutilización de conexiones
            var httpClient = _httpClientFactory.CreateClient();

            // Agrega el encabezado requerido para autorizar el acceso al flujo de Power Automate
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

            // Se ejecuta la solicitud GET hacia el endpoint configurado
            var httpResponse = await httpClient.GetAsync(url);

            // Se verifica que la respuesta sea exitosa antes de intentar deserializar el contenido
            if (!httpResponse.IsSuccessStatusCode)
                throw new HttpRequestException($"Error al consumir el flujo de estudiantes. Código: {httpResponse.StatusCode}");

            var payload = await httpResponse.Content.ReadFromJsonAsync<PowerAutomateResponse<ExcelStudentModel>>();

            // Se obtiene el array con los estudiantes; si vienen nulos se usa una lista vacía
            var students = payload?.Data?.Body ?? new List<ExcelStudentModel>();

            // Se normalizan los valores de cada estudiante antes de devolver el resultado final
            return students
                .Where(s => s != null)
                .Select(NormalizeStudent)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExcelSync] Error obteniendo estudiantes desde Power Automate: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene y normaliza la lista de mentores proporcionada por el flujo de Power Automate.
    /// </summary>
    /// <returns>
    /// Lista normalizada de objetos <see cref="ExcelMentorModel"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando la URL del flujo o la clave del API no están configuradas.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Se produce cuando ocurre un error al realizar la solicitud HTTP.
    /// </exception>
    private async Task<List<ExcelMentorModel>> GetMentorsFromExcelAsync()
    {
        // Obtiene el endpoint configurado que expone la información de mentores desde Power Automate
        var url = _configuration["ExcelSync:MentorsUrl"];

        // Obtiene la clave requerida por el flujo para autenticación vía header personalizado
        var apiKey = _configuration["ExcelSync:ApiKey"];

        // Se valida la existencia de la URL antes de realizar el consumo del servicio
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("La URL de sincronización de mentores no está configurada.");

        // Se valida que la API Key exista ya que es obligatoria para poder consumir el endpoint
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("La clave del API (ExcelSync:Key) no está configurada.");

        try
        {
            // Se obtiene una instancia de HttpClient desde el factory para reutilización de conexiones
            var httpClient = _httpClientFactory.CreateClient();

            // Agrega el encabezado requerido para autorizar el acceso al flujo de Power Automate
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

            // Se ejecuta la solicitud GET hacia el endpoint configurado
            var httpResponse = await httpClient.GetAsync(url);

            // Se verifica que la respuesta sea exitosa antes de intentar deserializar el contenido
            if (!httpResponse.IsSuccessStatusCode)
                throw new HttpRequestException($"Error al consumir el flujo de mentores. Código: {httpResponse.StatusCode}");

            var payload = await httpResponse.Content.ReadFromJsonAsync<PowerAutomateResponse<ExcelMentorModel>>();

            // Se obtiene el array con los estudiantes; si vienen nulos se usa una lista vacía
            var mentors = payload?.Data?.Body ?? new List<ExcelMentorModel>();

            // Se normalizan los valores de cada estudiante antes de devolver el resultado final
            return mentors
                .Where(m => m != null)
                .Select(NormalizeMentor)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExcelSync] Error obteniendo mentores desde Power Automate: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza el EntraUserId de un usuario utilizando su correo electrónico como identificador.
    /// </summary>
    /// <param name="email">Correo del usuario que será actualizado.</param>
    /// <param name="entraUserId">Nuevo identificador de Entra ID.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando los parámetros proporcionados no son válidos.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios en la base de datos.
    /// </exception>
    private async Task UpdateUserEntraUserIdAsync(string email, string entraUserId)
    {
        // Se validan los parámetros recibidos antes de ejecutar la operación
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo del usuario es obligatorio.", nameof(email));

        if (string.IsNullOrWhiteSpace(entraUserId))
            throw new ArgumentException("El EntraUserId es obligatorio.", nameof(entraUserId));

        // Se normaliza el correo para evitar problemas de coincidencia
        var normalizedEmail = email.Trim().ToLower();

        try
        {
            // Se busca el usuario correspondiente en la base de datos
            var mentor = await _context.UserTables
                .FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail);

            // Si existe, se actualiza el EntraUserId y metadatos de auditoría
            if (mentor != null)
            {
                mentor.EntraUserId = entraUserId;
                mentor.UpdatedAt = DateTime.UtcNow;
                mentor.UpdatedBy = _serviceAccount.Email;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura cualquier error ocurrido durante la actualización
            Console.WriteLine($"Error actualizando EntraUserId para email={email}. Detalle: {ex}");
            throw;
        }
    }  

    /// <summary>
    /// Normaliza los valores de un estudiante proveniente de Excel aplicando reglas de formato,
    /// tales como conversión a TitleCase y estandarización de correos electrónicos.
    /// </summary>
    /// <param name="student">Modelo original que contiene los datos del estudiante.</param>
    /// <returns>
    /// Un nuevo <see cref="ExcelStudentModel"/> con los valores normalizados.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando el modelo proporcionado es nulo.
    /// </exception>
    public ExcelStudentModel NormalizeStudent(ExcelStudentModel student)
    {
        // Se valida que el modelo recibido no sea nulo antes de iniciar la normalización
        if (student == null)
            throw new ArgumentNullException(nameof(student), "El modelo del estudiante no puede ser nulo.");

        // Se normalizan los campos removiendo espacios y aplicando formato consistente
        return new ExcelStudentModel
        {
            FullName = ToTitleCase(student.FullName?.Trim()),
            PreferredName = ToTitleCase(student.PreferredName?.Trim()),
            StudentEmail = student.StudentEmail != null
                    ? student.StudentEmail.Trim().ToLowerInvariant()
                    : null,
            MentorEmail = student.MentorEmail != null
                    ? student.MentorEmail.Trim().ToLowerInvariant()
                    : null,
            Status = ToTitleCase(student.Status?.Trim()),
            Type = ToTitleCase(student.Type?.Trim()),
            SpecialConsideration = student.SpecialConsideration,
            Gender = student.Gender
        };
    }

    /// <summary>
    /// Normaliza los valores de un mentor proveniente de Excel aplicando reglas consistentes
    /// como conversión a TitleCase, eliminación de espacios y estandarización de correos.
    /// </summary>
    /// <param name="mentor">Modelo original con los datos del mentor.</param>
    /// <returns>
    /// Un nuevo <see cref="ExcelMentorModel"/> con los valores normalizados.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando el modelo proporcionado es nulo.
    /// </exception>
    private ExcelMentorModel NormalizeMentor(ExcelMentorModel mentor)
    {
        // Se valida que el modelo recibido no sea nulo
        if (mentor == null)
            throw new ArgumentNullException(nameof(mentor), "El modelo del mentor no puede ser nulo.");

        // Se normalizan los campos de texto aplicando Trim y TitleCase,
        // y los correos se estandarizan utilizando ToLowerInvariant
        return new ExcelMentorModel
        {
            FullName = ToTitleCase(mentor.FullName?.Trim()),
            Email = mentor.Email != null
                ? mentor.Email.Trim().ToLowerInvariant()
                : null,
            Role = ToTitleCase(mentor.Role?.Trim()),
            BackupEmail = mentor.BackupEmail != null
                ? mentor.BackupEmail.Trim().ToLowerInvariant()
                : null,
            Status = ToTitleCase(mentor.Status?.Trim()),
            Type = ToTitleCase(mentor.Type?.Trim()),

            // Las vacaciones se dejan tal cual; la validación ocurre en otro método
            Vacation1 = mentor.Vacation1,
            Vacation2 = mentor.Vacation2
        };
    }

    /// <summary>
    /// Convierte una cadena a formato TitleCase aplicando mayúscula inicial
    /// y minúsculas para el resto de cada palabra.
    /// </summary>
    /// <param name="input">Cadena original a normalizar.</param>
    /// <returns>
    /// Una cadena con cada palabra en formato TitleCase, o el valor original si es nulo o vacío.
    /// </returns>
    private string ToTitleCase(string input)
    {
        // Se valida que la entrada tenga contenido antes de aplicar formato
        if (string.IsNullOrWhiteSpace(input))
            return input;

        input = input.Trim();

        // Se separa la cadena en palabras eliminando espacios repetidos
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Se construye cada palabra con mayúscula inicial y resto minúsculas
        var normalizedWords = words.Select(word =>
        {
            if (word.Length == 1)
                return char.ToUpperInvariant(word[0]).ToString();

            return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
        });

        // Se reconstruye la frase normalizada
        return string.Join(" ", normalizedWords);
    }
}
