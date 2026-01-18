using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;

public class UserService : IUserService
{
    private readonly DBContext _context;
    private readonly IBackendApiClientService _backendApiClient;
    private readonly StudentInformationRefreshOptions _studentInformationRefreshOptions;

    public UserService(
        DBContext context,
        IBackendApiClientService backendApiClient,
        IOptions<StudentInformationRefreshOptions> studentInformationRefreshOptions)
    {
        _context = context;
        _backendApiClient = backendApiClient;
        _studentInformationRefreshOptions = studentInformationRefreshOptions.Value;
    }

    public async Task<List<UserTable>> GetLocalUsersAsync(string role, string type)
    {
        return await _context.UserTables
            .Where(u => u.UserRole == role && u.UserType == type)
            .ToListAsync();
    }  

    public async Task UpdateStudentBannerFieldsAsync(
        string studentEmail,
        string? bannerId,
        string? pidm,
        string? identification,
        string? career)
    {
        var user = await _context.UserTables
            .FirstOrDefaultAsync(u => u.Email == studentEmail);

        if (user is null)
        {
            // Si prefieres, lanza una excepción o registra un warning
            return;
        }

        // Ajusta los nombres de propiedades a los de tu entidad UserTable
        user.BannerId = bannerId;
        user.Pidm = pidm;
        user.Identification = identification;
        user.Career = career;

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetUserRoleAsync(Data.DB.EF.VirtualMentorDB.Entities.Chat chat, string senderEntraUserId)
    {
        var senderDB = await _context.UserTables
            .FirstOrDefaultAsync(u => u.EntraUserId == senderEntraUserId);

        if (senderDB == null)
        {
            Console.WriteLine($"No se encontró el usuario emisor del mensaje | SenderEntraUserId={senderEntraUserId}");
            return "Desconocido";
        }

        // Si es Mentor y la IA está activa, marcar como "IA"
        if (senderDB.UserRole == "Mentor" && chat.Iaenabled)
        {
            return "IA";
        }

        return senderDB.UserRole;
    }

    public async Task<UserTable?> GetStudentByEntraIdAsync(string entraUserId)
    {
        return await _context.UserTables
            .FirstOrDefaultAsync(u =>
                u.EntraUserId == entraUserId &&
                u.UserRole == "Estudiante");
    }

    public async Task<(int processed, int updated)> SyncStudentInfoAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        int processed = 0;
        int updatedCount = 0;

        // Obtener lista de estudiantes locales
        var students = await GetLocalUsersAsync("Estudiante", "Banner");
        Console.WriteLine($"[StudentInfoRefresh] Estudiantes encontrados: {students.Count}");

        // Procesamiento en lotes
        var batchSize = Math.Max(1, _studentInformationRefreshOptions.BatchSize);

        for (int i = 0; i < students.Count; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var batch = students.Skip(i).Take(batchSize).ToList();

            foreach (var student in batch)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var email = student.Email;
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    // Obtener datos desde Banner via conector
                    var data = await _backendApiClient.GetStudentBannerDataAsync(email);
                    if (data == null)
                        continue;

                    // Tomamos la primera carrera enviada por Banner
                    var career = data.Programs?.FirstOrDefault();

                    // Si hay cambios pendientes -> actualizar en base local
                    if (NeedsUpdate(student, data, career))
                    {
                        await UpdateStudentBannerFieldsAsync(
                            student.Email,
                            bannerId: data.BannerId,
                            pidm: data.Pidm,
                            identification: data.PersonId,
                            career: career);

                        updatedCount++;
                    }

                    processed++;

                    // Control de frecuencia de solicitudes para evitar saturar servicios
                    if (_studentInformationRefreshOptions.DelayBetweenRequestsMs > 0)
                        await Task.Delay(_studentInformationRefreshOptions.DelayBetweenRequestsMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StudentInfoRefresh] Error con estudiante Id={student.Id}, Email={student.Email}: {ex.Message}");
                }
            }
        }

        stopwatch.Stop();

        Console.WriteLine($"[StudentInfoRefresh] Procesados: {processed}, Actualizados: {updatedCount}, Tiempo: {stopwatch.Elapsed}");

        return (processed, updatedCount);
    }

    public async Task<List<string>> GetActiveMentorEntraIdsAsync()
    {
        return await _context.UserTables
            .AsNoTracking()
            .Where(u => u.UserRole == "Mentor"
                     && u.UserState == "Activo"
                     && u.EntraUserId != null)
            .Select(u => u.EntraUserId!)
            .ToListAsync();
    }

    public async Task<List<string>> GetInactiveMentorEntraIdsAsync()
    {
        return await _context.UserTables
            .AsNoTracking()
            .Where(u => u.UserRole == "Mentor"
                     && u.UserState == "Inactivo"
                     && u.EntraUserId != null)
            .Select(u => u.EntraUserId!)
            .ToListAsync();
    }

    // Compara y decide si hay cambios que guardar
    private static bool NeedsUpdate(UserTable u, StudentBannerData d, string? career)
    {
        return !Eq(u.BannerId, d.BannerId)
            || !Eq(u.Pidm, d.Pidm)
            || !Eq(u.Identification, d.PersonId)
            || !Eq(u.Career, career);
    }

    private static bool Eq(string? a, string? b)
        => string.Equals(N(a), N(b), StringComparison.OrdinalIgnoreCase);

    private static string N(string? s) => (s ?? string.Empty).Trim();
}
