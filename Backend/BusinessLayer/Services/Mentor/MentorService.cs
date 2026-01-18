using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;

public class MentorService : IMentorService
{

    private readonly IChatService _chatService;
    private readonly ITokenStoreService _tokenStoreService;
    private readonly ISalesforceApiClient _salesforceApiClient;
    private readonly ISalesforceTokenProvider _tokenProvider;
    private readonly IMicrosoftGraphService _microsoftgraphService;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly DBContext _context;
    private readonly IConfiguration _configuration;

    public MentorService(
        IChatService chatService,
        ITokenStoreService tokenStoreService,
        ISalesforceApiClient salesforceApiClient,
        ISalesforceTokenProvider tokenProvider,
        IMicrosoftGraphService microsoftGraphService,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        DBContext context,
        IConfiguration configuration)
    {
        _chatService = chatService;
        _tokenStoreService = tokenStoreService;
        _salesforceApiClient = salesforceApiClient;
        _tokenProvider = tokenProvider;
        _microsoftgraphService = microsoftGraphService;
        _serviceAccount = serviceAccountOptions.Value;
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Obtiene la información de un mentor almacenado en Salesforce utilizando una consulta SOQL.
    /// La consulta se arma dinámicamente reemplazando el correo electrónico en la plantilla definida en la configuración.
    /// </summary>
    /// <param name="email">Dirección de correo del mentor a consultar.</param>
    /// <returns>
    /// Un objeto <see cref="GeneralSalesforceUserResponse"/> con los datos obtenidos desde Salesforce.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se lanza cuando faltan parámetros de configuración, la plantilla SOQL no existe,
    /// o no se puede obtener un token válido para Salesforce.
    /// </exception>
    /// <exception cref="Exception">
    /// Repropaga cualquier excepción ocurrida en la llamada HTTP o en la deserialización.
    /// </exception>
    public async Task<GeneralSalesforceUserResponse> GetMentorAsync(string email)
    {
        // Validación inicial del parámetro email
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email del mentor es requerido.", nameof(email));

        try
        {
            // Recupera la plantilla SOQL desde configuración
            string template = _configuration["Salesforce:Querys:GetMentor"] ?? string.Empty;

            // Validación de existencia de plantilla SOQL
            if (string.IsNullOrWhiteSpace(template))
                throw new InvalidOperationException("No se encontró Salesforce:Querys:GetMentor en configuración.");

            // Reemplaza marcador {email} por el valor real
            var soql = template.Replace("{email}", email);

            // Obtiene valores base de configuración de Salesforce
            string baseUrl = _configuration["Salesforce:BaseUrl"];
            string apiBase = _configuration["Salesforce:RequestUri:ServiceVersion"];

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("No se encontró Salesforce:BaseUrl en configuración.");

            if (string.IsNullOrWhiteSpace(apiBase))
                throw new InvalidOperationException("No se encontró Salesforce:RequestUri:ServiceVersion en configuración.");

            // Obtiene un access token válido para Salesforce
            string accessToken = await _tokenProvider.GetTokenAsync();

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("No se obtuvo access token de Salesforce.");

            // Construye el endpoint completo para ejecutar la query SOQL
            string requestUri = $"{apiBase}/query?q={Uri.EscapeDataString(soql)}";

            // Realiza la llamada HTTP GET mediante el cliente reutilizable de Salesforce
            var json = await _salesforceApiClient.GetAsync(requestUri, accessToken);

            // Deserializa la respuesta
            var mentorResponse =
                JsonConvert.DeserializeObject<GeneralSalesforceUserResponse>(json)
                ?? new GeneralSalesforceUserResponse();

            return mentorResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Salesforce:GetMentorAsync] Error consultando mentor {email}: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene la lista de estudiantes asociados a un mentor específico.
    /// Antes de obtenerlos, almacena de manera segura el token proporcionado (si existe)
    /// usando un nombre de clave normalizado basado en el correo del mentor.
    /// </summary>
    /// <param name="mentorEmail">Correo del mentor cuyos estudiantes deben consultarse.</param>
    /// <param name="token">
    /// Token opcional que será guardado previo a la consulta. 
    /// Si es <c>null</c>, solo se realiza la consulta.
    /// </param>
    /// <returns>
    /// Una lista de objetos <see cref="StudentChatDto"/> representando los estudiantes asociados.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se lanza si el correo del mentor es nulo o vacío.
    /// </exception>
    public async Task<List<StudentChatDto>> GetStudentsByMentorEmailAsync(
        string mentorEmail, 
        string? token)
    {
        if (string.IsNullOrWhiteSpace(mentorEmail))
            throw new ArgumentException("El correo del mentor es requerido.", nameof(mentorEmail));

        // Normaliza la clave que se usará para almacenar el token
        var secretName = NormalizeKey(mentorEmail);

        // Si hay token, se guarda utilizando el store seguro
        // Si token == null el método interno decidirá si sobrescribe o ignora
        await _tokenStoreService.StoreTokenAsync(secretName, token);

        // Llamada al método real que obtiene a los estudiantes
        return await GetStudentsByMentorEmailAsync(mentorEmail);
    }

    /// Obtiene la lista de estudiantes asociados al mentor especificado por su correo electrónico.
    /// Incluye información del chat activo, estado de la IA, último mensaje y lectura.
    /// </summary>
    /// <param name="mentorEmail">Correo electrónico del mentor.</param>
    /// <returns>
    /// Lista de <see cref="MentorDto"/> (que contiene información del estudiante y su chat asociado).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se lanza si <paramref name="mentorEmail"/> es nulo o vacío.
    /// </exception>
    public async Task<List<StudentChatDto>> GetStudentsByMentorEmailAsync(string mentorEmail)
    {
        // Validación básica del parámetro de entrada
        if (string.IsNullOrWhiteSpace(mentorEmail))
            throw new ArgumentException("El correo del mentor es requerido.", nameof(mentorEmail));

        // Consulta principal:
        // Estudiantes --> Chats --> Mentor asignado.
        // Se utiliza LINQ-to-Entities para obtener todos los estudiantes que tienen un chat con este mentor.
        var students = await (
            from student in _context.UserTables
            where student.UserRole == "Estudiante"

            join chat in _context.Chats 
                on student.Id equals chat.StudentId

            join mentor in _context.UserTables 
                on chat.MentorId equals mentor.Id

            where mentor.UserRole == "Mentor" 
            && mentor.Email == mentorEmail

            select new StudentChatDto
            {
                Id = student.Id,
                FullName = student.FullName,
                Email = student.Email,

                // Estado de lectura del chat
                IsRead = chat.IsRead,

                ChatId = chat.MsteamsChatId ?? string.Empty,

                // Estado de IA del chat (se toma directamente del registro del chat)
                AIState = chat.Iaenabled,

                AIChangeReason = _context.ChatIalogs
                    .Where(c => c.ChatId == chat.Id)
                    .OrderByDescending(c => c.UpdatedAt)
                    .Select(c => c.IachangeReason)
                    .FirstOrDefault() ?? string.Empty,

                // Fecha del último mensaje enviado dentro de las conversaciones del chat
                LastMessageDate = _context.Messages
                .Where(m => _context.Conversations
                    .Where(c => c.ChatId == chat.Id)
                    .Select(c => c.Id)
                    .Contains(m.ConversationId))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefault(),

                // Contenido del último mensaje
                LastMessageContent = _context.Messages
                .Where(m => _context.Conversations
                    .Where(c => c.ChatId == chat.Id)
                    .Select(c => c.Id)
                    .Contains(m.ConversationId))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.MessageContent ?? string.Empty)
                .FirstOrDefault()
            }
        ).ToListAsync();

        return students;
    }

    /// <summary>
    /// Obtiene el tipo de mentor (UserType) asociado a un correo electrónico.
    /// </summary>
    /// <param name="email">Correo del mentor o administrador.</param>
    /// <returns>
    /// Tipo de usuario (UserType) si existe uno activo con rol Mentor o Admin; 
    /// de lo contrario, <c>null</c>.
    /// </returns>
    public async Task<MentorTypeDto?> GetMentorTypeByEmailAsync(string email)
    {
        // Validación defensiva: evita procesamiento innecesario
        if (string.IsNullOrWhiteSpace(email))
            return null;

        // Normalizar email para evitar problemas con espacios o mayúsculas
        email = email.Trim();

        // Validar formato de correo (si falla, se retorna null)
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
        }
        catch
        {
            return null;
        }

        // Consulta al usuario activo cuyo rol sea Mentor o Admin
        // Se selecciona directamente el tipo de usuario (UserType)
        return await _context.UserTables
            .Where(u =>
                u.Email == email &&
                u.UserState == "Activo" &&
                (u.UserRole == "Mentor" || u.UserRole == "Admin"))
            .Select(u => new MentorTypeDto
            {
                EntraId = u.EntraUserId,
                MentorType = u.UserType
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Obtiene el tipo de mentor asociado a un correo electrónico.
    /// Permite recibir un token opcional que es almacenado antes de ejecutar la lógica principal.
    /// </summary>
    /// <param name="email">Correo del mentor.</param>
    /// <param name="token">
    /// Token opcional enviado desde el frontend para su almacenamiento
    /// </param>
    /// <returns>
    /// Un objeto <see cref="MentorTypeDto"/> con el tipo de mentor encontrado,
    /// o <c>null</c> si el mentor no existe.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Lanzado cuando el email es nulo o vacío.
    /// </exception>
    public async Task<MentorTypeDto?> GetMentorTypeByEmailAsync(string email, string? token = null)
    {
        // Validación temprana
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email no puede ser nulo o vacío.", nameof(email));

        // Normaliza la clave para almacenamiento seguro
        var secretName = NormalizeKey(email);

        // Guarda el token si corresponde
        await _tokenStoreService.StoreTokenAsync(secretName, token);

        // Obtener el tipo de mentor desde el método real
        var mentorInfo = await GetMentorTypeByEmailAsync(email);

        // Si no existe información, retornar null
        if (mentorInfo == null)
            return null;

        // Crear y retornar DTO de respuesta
        return new MentorTypeDto
        {
            EntraId = mentorInfo.EntraId,
            MentorType = mentorInfo.MentorType
        };
    }

    /// <summary>
    /// Realiza una búsqueda paginada de mensajes asociados a un mentor, filtrando por contenido
    /// del mensaje o por el nombre del estudiante. Incluye paginación y verificación del mentor.
    /// </summary>
    /// <param name="request">
    /// Objeto <see cref="SearchMessagesRequest"/> que contiene el correo del mentor, 
    /// el término de búsqueda, y los parámetros de paginación.
    /// </param>
    /// <returns>
    /// Un <see cref="PagedResultDto{SearchMessagesDto}"/> con los resultados paginados.
    /// Si el mentor no existe o la consulta no devuelve datos, retorna un objeto con Total=0.
    /// </returns>
    public async Task<PagedResultDto<SearchMessagesDto>> SearchMessagesAsync(SearchMessagesRequest request)
    {
        // Validación de parámetros básicos
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.MentorEmail))
            return new PagedResultDto<SearchMessagesDto>();

        // Normalizar paginación
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        // Normalizar Query (evita LIKE wide match con nulls)
        var query = request.Query?.Trim() ?? string.Empty;

        // Buscar mentor activo
        var mentor = await _context.UserTables
            .AsNoTracking()
            .FirstOrDefaultAsync(u => 
                u.Email == request.MentorEmail && 
                u.UserRole == "Mentor" &&
                u.UserState == "Activo");

        if (mentor == null)
            return new PagedResultDto<SearchMessagesDto>();

        // Query base con joins
        var baseQuery = 
            from chat in _context.Chats
            join student in _context.UserTables on chat.StudentId equals student.Id
            join conversation in _context.Conversations on chat.Id equals conversation.ChatId
            join message in _context.Messages on conversation.Id equals message.ConversationId
            where chat.MentorId == mentor.Id &&
                    (
                        // Buscar en mensaje
                        EF.Functions.Like(message.MessageContent ?? "", $"%{query}%") 
                        ||
                        // Buscar por nombre del estudiante
                        EF.Functions.Like(student.FullName ?? "", $"%{query}%"))
            select new SearchMessagesDto
            {
                Id = message.Id,
                SenderRole = message.SenderRole,
                StudentFullName = student.FullName,
                ChatId = chat.MsteamsChatId ?? string.Empty,
                Content = message.MessageContent,
                ContentType = message.MessageContentType,
                Date = message.UpdatedAt
            };

        // Total antes de paginación
        var total = await baseQuery.CountAsync();

        // Si no hay datos, ahorrar recursos
        if (total == 0)
        {
            return new PagedResultDto<SearchMessagesDto>
            {
                Total = 0,
                Page = page,
                PageSize = pageSize,
                Results = new List<SearchMessagesDto>()
            };
        }

        // Recuperar página
        var results = await baseQuery
            .OrderByDescending(m => m.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<SearchMessagesDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Results = results
        };
    }

    /// <summary>
    /// Envía mensajes proactivos a los estudiantes en caso de que su chat de Microsoft Teams
    /// aún no tenga un MSTeamsChatId registrado. Este método:
    /// 1. Obtiene todos los chats asociados a un mentor.
    /// 2. Verifica si el chat no tiene un MSTeamsChatId.
    /// 3. Si falta, envía un mensaje al estudiante para crear el chat.
    /// 4. Actualiza el MSTeamsChatId en la base de datos.
    /// </summary>
    /// <param name="email">Correo electrónico del mentor.</param>
    public async Task SendProactiveMessagesIfNecessaryAsync(string email)
    {
        // Validar email de entrada
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.WriteLine("[PROACTIVE] Email del mentor es nulo o vacío.");
            return;
        }

        // Obtener los chats del mentor desde la base de datos
        var chats = await _chatService.GetChatsByMentorEmailAsync(email);

        if (chats == null || !chats.Any())
        {
            Console.WriteLine($"[PROACTIVE] No se encontraron chats para el mentor {email}");
            return;
        }

        foreach (var chat in chats)
        {
            try
            {
                // Si ya tiene MSTeamsChatId, no se realiza ninguna acción
                if (!string.IsNullOrWhiteSpace(chat.MsteamsChatId))
                    continue;

                // Validar referencias de navegación
                var mentorId = chat.Mentor?.EntraUserId;
                var studentId = chat.Student?.EntraUserId;

                if (string.IsNullOrWhiteSpace(mentorId) || string.IsNullOrWhiteSpace(studentId))
                {
                    Console.WriteLine($"[PROACTIVE] Faltan EntraUserIds (MentorId o StudentId) para chat del mentor {email}.");
                    continue;
                }
                
                // Buscar contenido del mensaje proactivo
                var content = await GetMessageByKeyAsync("MissingTeamsChatId");

                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine("[PROACTIVE] No se encontró mensaje configurado para MissingTeamsChatId.");
                    continue;
                }

                // Enviar mensaje proactivo vía Microsoft Graph
                var newChatId = await _microsoftgraphService.SendMessageToUserAsync(mentorId, studentId, content);

                if (string.IsNullOrWhiteSpace(newChatId))
                {
                    Console.WriteLine($"[PROACTIVE] No se pudo obtener un nuevo MSTeamsChatId para mentor {mentorId}, estudiante {studentId}");
                    continue;
                }

                // Actualizar MSTeamsChatId en BD
                await _chatService.UpdateMsTeamsChatIdAsync(chat.MentorId, chat.StudentId, newChatId);

                Console.WriteLine($"[PROACTIVE] MSTeamsChatId actualizado correctamente: {newChatId}");
            }
            catch (Exception ex)
            {
                // Manejar errores por chat sin detener todo el flujo
                Console.WriteLine($"[PROACTIVE] Error procesando chat del mentor {email}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Obtiene el contenido de un mensaje proactivo configurado en la base de datos,
    /// identificado por una clave única (<paramref name="messageKey"/>).
    /// </summary>
    /// <param name="messageKey">
    /// Clave del mensaje buscado (por ejemplo: "MissingTeamsChatId").
    /// </param>
    /// <returns>
    /// El contenido del mensaje si existe; de lo contrario, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Este método se utiliza para obtener textos predefinidos que serán enviados
    /// automáticamente en flujos proactivos.
    /// </remarks>
    public async Task<string?> GetMessageByKeyAsync(string messageKey)
    {
        // Validación temprana para evitar consultas innecesarias
        if (string.IsNullOrWhiteSpace(messageKey))
        {
            Console.WriteLine("[ProactiveMessages] Clave de mensaje vacía o nula.");
            return null;
        }

        // Consulta que selecciona el contenido del mensaje
        var messageContent = await _context.ProactiveMessages
            .Where(p => p.MessageKey == messageKey)
            .Select(p => p.MessageContent)
            .FirstOrDefaultAsync();

        if (messageContent == null)
        {
            Console.WriteLine($"[ProactiveMessages] No se encontró mensaje con clave '{messageKey}'.");
        }

        return messageContent;
    }    

    /// <summary>
    /// Obtiene la lista de mentores que poseen un correo de respaldo,
    /// filtrados por el estado especificado.
    /// </summary>
    /// <param name="state">Estado del mentor a filtrar.</param>
    /// <returns>
    /// Lista de entidades <see cref="UserTable"/> correspondientes a mentores que cumplen el criterio.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el estado proporcionado no es válido.
    /// </exception>
    public async Task<List<UserTable>> GetMentorsWithBackupEmailAsync(string state)
    {
        // Se valida que el estado sea válido antes de ejecutar la consulta
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("El estado es obligatorio.", nameof(state));

        // Se normaliza el estado recibido para evitar inconsistencias al filtrar
        var normalizedState = state.Trim();

        // Consulta que obtiene mentores activos con correo de respaldo configurado
        // Se incluye la relación UserLeaves para contar con información asociada al mentor
        return await _context.UserTables
            .AsNoTracking()
            .Include(u => u.UserLeaves)
            .Where(u =>
                (u.UserRole == "Mentor") &&
                u.UserState == normalizedState &&
                !string.IsNullOrEmpty(u.BackupEmail))
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene un mentor cuyo correo coincide con el proporcionado,
    /// filtrando exclusivamente por rol Mentor.
    /// </summary>
    /// <param name="email">Correo electrónico del mentor.</param>
    /// <returns>
    /// La entidad <see cref="UserTable"/> correspondiente al mentor,
    /// o null si no existe coincidencia.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el correo proporcionado no es válido.
    /// </exception>
    public async Task<UserTable?> GetBackupMentorByEmailAsync(string email)
    {
        // Se valida que el correo sea válido antes de ejecutar la consulta
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo del mentor es obligatorio.", nameof(email));

        // Se normaliza el correo recibido para evitar inconsistencias al comparar
        var normalizedEmail = email.Trim().ToLower();

        // Consulta que obtiene al mentor asociado al correo especificado
        return await _context.UserTables
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedEmail &&
                (u.UserRole == "Mentor"));
    }

    /// <summary>
    /// Obtiene el mentor asociado al estudiante a partir del chat activo más reciente.
    /// </summary>
    /// <param name="studentId">Identificador interno del estudiante.</param>
    /// <returns>
    /// La entidad <see cref="UserTable"/> correspondiente al mentor,
    /// o null si no existe un chat activo o no hay mentor asignado.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador del estudiante no es válido.
    /// </exception>
    public async Task<UserTable?> GetMentorByStudentIdAsync(int studentId)
    {
        // Se valida que el identificador del estudiante sea válido antes de ejecutar la consulta
        if (studentId <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(studentId));

        // Consulta que obtiene el chat activo más reciente del estudiante y extrae el mentor asociado
        // Se retorna null si no existe un mentor asignado en el chat encontrado
        return await _context.Chats
            .AsNoTracking()
            .Where(c => c.StudentId == studentId && c.ChatState == "Activo")
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Mentor)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Valida una lista de mentores provenientes de Excel y construye un detalle
    /// de aquellos que presentan inconsistencias en sus campos obligatorios.
    /// </summary>
    /// <param name="mentors">Lista de mentores a validar.</param>
    /// <returns>
    /// Lista de objetos <see cref="InvalidMentorModel"/> con el detalle de los campos faltantes o inválidos.
    /// </returns>
    public List<InvalidMentorModel> GetInvalidMentorsDetailed(List<ExcelMentorModel> mentors)
    {
        // Se valida que la lista de mentores sea válida antes de iniciar el procesamiento
        if (mentors == null || mentors.Count == 0)
            return new List<InvalidMentorModel>();

        var invalidMentors = new List<InvalidMentorModel>();

        // Se recorre cada mentor para identificar campos obligatorios faltantes o con formato incorrecto
        foreach (var mentor in mentors)
        {
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(mentor.FullName))
                missingFields.Add("FullName");

            if (string.IsNullOrWhiteSpace(mentor.Role))
                missingFields.Add("Role");

            if (string.IsNullOrWhiteSpace(mentor.Type))
                missingFields.Add("Type");

            if (string.IsNullOrWhiteSpace(mentor.Status))
                missingFields.Add("Status");

            // Se valida el correo principal
            if (string.IsNullOrWhiteSpace(mentor.Email))
                missingFields.Add("Email");
            else
            {
                try
                {
                    var addr = new System.Net.Mail.MailAddress(mentor.Email);

                    if (addr.Address != mentor.Email)
                        missingFields.Add("Email (formato inválido)");
                }
                catch
                {
                    missingFields.Add("Email (formato inválido)");
                }
            }

            // Se valida el correo de respaldo si existe
            if (!string.IsNullOrWhiteSpace(mentor.BackupEmail))
            {
                try
                {
                    var addr = new System.Net.Mail.MailAddress(mentor.BackupEmail);

                    if (addr.Address != mentor.BackupEmail)
                        missingFields.Add("BackupEmail (formato inválido)");
                }
                catch
                {
                    missingFields.Add("BackupEmail (formato inválido)");
                }
            }

            // Se agregan errores provenientes de las validaciones de vacaciones
            var (validVac1, vac1Errors) = ValidateVacation(mentor.Vacation1, "Vacation1");
            var (validVac2, vac2Errors) = ValidateVacation(mentor.Vacation2, "Vacation2");

            missingFields.AddRange(vac1Errors);
            missingFields.AddRange(vac2Errors);

            // Si existen errores para el mentor actual, se agrega un registro detallado en la lista de resultados
            if (missingFields.Any())
            {
                invalidMentors.Add(new InvalidMentorModel
                {
                    Email = mentor.Email,
                    FullName = mentor.FullName,
                    MissingFields = string.Join(", ", missingFields)
                });
            }
        }

        return invalidMentors;
    }

    /// <summary>
    /// Crea un nuevo mentor en la base de datos a partir de los datos obtenidos desde Excel.
    /// </summary>
    /// <param name="dto">Modelo con la información del mentor proveniente del archivo Excel.</param>
    /// <returns>
    /// Una tarea asincrónica que finaliza cuando el mentor y sus vacaciones válidas han sido registradas.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando el modelo es nulo o no contiene información esencial.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar la información en la base de datos.
    /// </exception>
    public async Task CreateMentorAsync(ExcelMentorModel dto)
    {
        // Se valida que el modelo recibido contenga la información mínima necesaria
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "El modelo del mentor no puede ser nulo.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("El correo del mentor es obligatorio.", nameof(dto.Email));

        // Se normaliza el correo electrónico del mentor para evitar inconsistencias
        var normalizedEmail = dto.Email.Trim().ToLower();

        try
        {
            // Se construye la entidad UserTable con los datos provenientes de Excel
            var newMentor = new UserTable
            {
                Email = normalizedEmail,
                Identification = dto.Identification,
                BannerId = dto.BannerId,
                Pidm = dto.Pidm,
                FullName = dto.FullName,
                FavoriteName = null,
                Gender = dto.Gender,
                UserRole = dto.Role,
                UserType = dto.Type,
                BackupEmail = dto.BackupEmail,
                UserState = dto.Status,
                CreatedBy = _serviceAccount.Email,
                UpdatedBy = _serviceAccount.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserTables.Add(newMentor);
            await _context.SaveChangesAsync();

            // Se procesan los rangos de vacaciones para crear los registros correspondientes
            var leaves = new List<UserLeave>();

            AddLeaveIfValid(dto.Vacation1, newMentor.Id, leaves);
            AddLeaveIfValid(dto.Vacation2, newMentor.Id, leaves);

            // Guardar vacaciones si hay
            if (leaves.Any())
            {
                _context.UserLeaves.AddRange(leaves);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura el error para evitar que el proceso se detenga
            Console.WriteLine($"Error creando mentor desde Excel (email={dto.Email}). Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Activa un mentor actualizando su estado a 'Activo' si no se encuentra ya en ese estado.
    /// </summary>
    /// <param name="id">Identificador interno del mentor.</param>
    /// <returns>Tarea asincrónica que finaliza cuando el cambio ha sido procesado.</returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador proporcionado no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task ActivateMentorAsync(int id)
    {
        // Se valida que el identificador proporcionado sea válido
        if (id <= 0)
            throw new ArgumentException("El identificador del mentor debe ser mayor a cero.", nameof(id));

        try
        {
            // Se busca el mentor en la base de datos
            var mentor = await _context.UserTables.FindAsync(id);

            // Si existe y no está activo, se actualiza su estado y metadatos de auditoría
            if (mentor != null && mentor.UserState != "Activo")
            {
                mentor.UserState = "Activo";
                mentor.UpdatedBy = _serviceAccount.Email;
                mentor.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error activando mentor con Id={id}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza la información de un mentor utilizando los datos provenientes de Excel,
    /// incluyendo datos generales y rangos de vacaciones.
    /// </summary>
    /// <param name="id">Identificador interno del mentor.</param>
    /// <param name="dto">Datos actualizados del mentor obtenidos desde Excel.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador es inválido.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando el modelo proporcionado es nulo.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task UpdateMentorAsync(int id, ExcelMentorModel dto)
    {
        // Se valida el identificador y el modelo antes de procesar la actualización
        if (id <= 0)
            throw new ArgumentException("El identificador del mentor debe ser mayor a cero.", nameof(id));

        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "El modelo del mentor no puede ser nulo.");

        try
        {
            // Se obtiene el mentor desde la base de datos
            var mentor = await _context.UserTables.FindAsync(id);
            if (mentor == null)
                return;

            bool needsUpdate = false;

            // Se comparan los campos principales para determinar si hay cambios reales
            if (mentor.FullName != dto.FullName)
            {
                mentor.FullName = dto.FullName;
                needsUpdate = true;
            }

            if (mentor.Gender != dto.Gender)
            {
                mentor.Gender = dto.Gender;
                needsUpdate = true;
            }

            if (mentor.UserRole != dto.Role)
            {
                mentor.UserRole = dto.Role;
                needsUpdate = true;
            }

            if (mentor.UserType != dto.Type)
            {
                mentor.UserType = dto.Type;
                needsUpdate = true;
            }

            if (mentor.UserState != dto.Status)
            {
                mentor.UserState = dto.Status;
                needsUpdate = true;
            }

            if (mentor.BackupEmail != dto.BackupEmail)
            {
                mentor.BackupEmail = dto.BackupEmail;
                needsUpdate = true;
            }

            // Se obtienen las vacaciones activas actuales en orden consistente
            var currentLeaves = await _context.UserLeaves
                .Where(l => l.MentorId == mentor.Id && l.UserLeaveState == "Activo")
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            var newLeaves = new List<(DateTime? Start, DateTime? End)>
        {
            (TryParseDate(dto.Vacation1?.Start), TryParseDate(dto.Vacation1?.End)),
            (TryParseDate(dto.Vacation2?.Start), TryParseDate(dto.Vacation2?.End))
        };

            bool vacationChanged = false;

            // Se comparan las vacaciones nuevas con las existentes para aplicar cambios
            for (int i = 0; i < 2; i++)
            {
                var (newStart, newEnd) = newLeaves[i];

                // Caso: vacaciones válidas nuevas
                if (newStart.HasValue && newEnd.HasValue)
                {
                    if (newStart > newEnd)
                        continue; // Se ignoran rangos inválidos

                    if (i < currentLeaves.Count)
                    {
                        var existing = currentLeaves[i];

                        if (existing.StartDate != newStart.Value || existing.EndDate != newEnd.Value)
                        {
                            existing.StartDate = newStart.Value;
                            existing.EndDate = newEnd.Value;
                            existing.UpdatedAt = DateTime.UtcNow;
                            existing.UpdatedBy = _serviceAccount.Email;
                            vacationChanged = true;
                        }
                    }
                    else
                    {
                        _context.UserLeaves.Add(new UserLeave
                        {
                            MentorId = mentor.Id,
                            StartDate = newStart.Value,
                            EndDate = newEnd.Value,
                            UserLeaveState = "Activo",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            CreatedBy = _serviceAccount.Email,
                            UpdatedBy = _serviceAccount.Email
                        });

                        vacationChanged = true;
                    }
                }
                // Caso: Excel no trae vacaciones en esta posición
                else if (i < currentLeaves.Count)
                {
                    var existing = currentLeaves[i];
                    existing.UserLeaveState = "Inactivo";
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = _serviceAccount.Email;
                    vacationChanged = true;
                }
            }

            // Si hubo cambios generales o en vacaciones, se guardan y actualizan metadatos
            if (needsUpdate || vacationChanged)
            {
                mentor.UpdatedBy = _serviceAccount.Email;
                mentor.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura y registra cualquier excepción
            Console.WriteLine($"Error actualizando mentor Id={id}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Desactiva a un mentor estableciendo su estado a 'Inactivo'.
    /// </summary>
    /// <param name="id">Identificador interno del mentor.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador proporcionado no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task DeactivateMentorAsync(int id)
    {
        // Se valida que el identificador proporcionado sea válido
        if (id <= 0)
            throw new ArgumentException("El identificador del mentor debe ser mayor a cero.", nameof(id));

        try
        {
            // Se obtiene el usuario desde la base de datos
            var mentor = await _context.UserTables.FindAsync(id);

            // Se confirma que el usuario existe y corresponde a un mentor
            if (mentor != null &&
                mentor.UserRole == "Mentor" &&
                mentor.UserState != "Inactivo")
            {
                // Si el mentor no está inactivo, se actualiza su estado y metadatos de auditoría
                mentor.UserState = "Inactivo";
                mentor.UpdatedBy = _serviceAccount.Email;
                mentor.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Se captura cualquier error ocurrido durante el guardado
            Console.WriteLine($"Error desactivando mentor Id={id}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Agrega un registro de vacaciones si las fechas proporcionadas son válidas.
    /// </summary>
    private void AddLeaveIfValid(object? vacation, int mentorId, List<UserLeave> leaves)
    {
        if (vacation == null)
            return;

        // Se obtienen propiedades Start y End mediante reflexión segura
        var startProp = vacation.GetType().GetProperty("Start");
        var endProp = vacation.GetType().GetProperty("End");

        if (startProp == null || endProp == null)
            return;

        var startValue = startProp.GetValue(vacation) as string;
        var endValue = endProp.GetValue(vacation) as string;

        if (string.IsNullOrWhiteSpace(startValue) || string.IsNullOrWhiteSpace(endValue))
            return;

        if (!DateTime.TryParse(startValue, out var startDate))
            return;

        if (!DateTime.TryParse(endValue, out var endDate))
            return;

        if (startDate > endDate)
            return;

        leaves.Add(new UserLeave
        {
            MentorId = mentorId,
            StartDate = startDate,
            EndDate = endDate,
            UserLeaveState = "Activo",
            CreatedBy = _serviceAccount.Email,
            UpdatedBy = _serviceAccount.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Valida un periodo de vacaciones verificando la presencia de fechas,
    /// el formato correcto (yyyy-MM-dd) y la coherencia del rango.
    /// </summary>
    /// <param name="vac">Objeto que contiene las fechas de inicio y fin del periodo.</param>
    /// <param name="label">Etiqueta utilizada para identificar el origen del error.</param>
    /// <returns>
    /// Una tupla que indica si el periodo es válido y una lista de mensajes descriptivos
    /// en caso de inconsistencias.
    /// </returns>
    private (bool isValid, List<string> errors) ValidateVacation(VacationPeriodDto vac, string label)
    {
        var errors = new List<string>();

        // Se valida si el objeto es nulo; si no hay periodo, no es considerado inválido
        if (vac == null)
            return (true, errors);

        bool isStartEmpty = string.IsNullOrWhiteSpace(vac.Start);
        bool isEndEmpty = string.IsNullOrWhiteSpace(vac.End);

        // Se valida la presencia de ambas fechas
        if (isStartEmpty || isEndEmpty)
        {
            errors.Add($"{label} incompleto");
            return (false, errors);
        }

        // Se valida el formato de las fechas
        bool isStartValid = DateTime.TryParseExact(
            vac.Start,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedStart
        );

        bool isEndValid = DateTime.TryParseExact(
            vac.End,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedEnd
        );

        if (!isStartValid)
            errors.Add($"{label}.Start con formato inválido (esperado yyyy-MM-dd)");

        if (!isEndValid)
            errors.Add($"{label}.End con formato inválido (esperado yyyy-MM-dd)");

        // Si ambas fechas tienen formato válido, se valida coherencia del rango
        if (isStartValid && isEndValid && parsedStart > parsedEnd)
            errors.Add($"{label}: la fecha de inicio no puede ser mayor que la fecha de fin");

        return (!errors.Any(), errors);
    }

    /// <summary>
    /// Normaliza una clave de entrada para que sea segura como nombre de secreto,
    /// evitando caracteres no permitidos o problemáticos en sistemas como Key Vault,
    /// Redis, Filesystem o bases de datos.
    /// </summary>
    /// <param name="rawKey">Cadena original, usualmente un correo electrónico.</param>
    /// <returns>
    /// Cadena normalizada y en minúsculas, reemplazando '@' por "-at-" y '.'
    /// por '-' para asegurar compatibilidad.
    /// </returns>
    public string NormalizeKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new ArgumentException("El valor de la clave no puede estar vacío.", nameof(rawKey));

        // Convertir todo a minúsculas para evitar claves duplicadas insensibles a mayúsculas.
        var normalized = rawKey.ToLowerInvariant();

        // Reemplazar caracteres problemáticos en sistemas de almacenamiento.
        // '@' puede causar problemas en rutas y nombres de secreto → se reemplaza por "-at-".
        // '.' puede generar conflictos en nombres de archivo o keys → se reemplaza por '-'.
        normalized = normalized.Replace("@", "-at-").Replace(".", "-");

        // Se retorna la clave normalizada, lista para usar como nombre único y estable.
        return normalized;
    }

    /// <summary>
    /// Intenta convertir una cadena al tipo DateTime utilizando el formato exacto yyyy-MM-dd.
    /// </summary>
    /// <param name="date">Cadena que representa una fecha.</param>
    /// <returns>
    /// La fecha convertida o null si no es posible analizarla.
    /// </returns>
    private DateTime? TryParseDate(string date)
    {
        // Se valida que la cadena tenga contenido antes de intentar convertirla
        if (string.IsNullOrWhiteSpace(date))
            return null;

        // Se intenta analizar la fecha utilizando el formato exacto esperado para evitar ambigüedades por cultura
        if (DateTime.TryParseExact(
                date.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            return result;
        }

        // Si la conversión falla se retorna null
        return null;
    }
}
