using Academikus.AgenteInteligenteMentoresTareas.Business.Common;
using Academikus.AgenteInteligenteMentoresTareas.Business.Hubs;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.AI;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.BannerWebApi;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.BatchService;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.DailySummaries;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Academikus.AgenteInteligenteMentoresTareas.Utility.General;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Conversations;

public class ConversationProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AiBatchingOptions _aiBatchingOptions;

    public ConversationProcessor(
        IServiceProvider serviceProvider,
        IOptions<AiBatchingOptions> aiBatchingOptions)
    {
        _serviceProvider = serviceProvider;
        _aiBatchingOptions = aiBatchingOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("AiBatchWorker iniciado.");

        while (true)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sp = scope.ServiceProvider;

                var batchService = sp.GetRequiredService<IAiBatchService>();
                var chatService = sp.GetRequiredService<IChatService>();
                var aiAgentService = sp.GetRequiredService<IaiClientService>();
                var backendApiClient = sp.GetRequiredService<IBackendApiClientService>();
                var chatSummaryService = sp.GetRequiredService<ISummaryService>();
                var bannerApiService = sp.GetRequiredService<IBannerWebApiService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();
                var configuration = sp.GetRequiredService<IConfiguration>();

                var pendingBatches = await batchService.TakeDueBatchesAsync(_aiBatchingOptions.MaxBatchesPerScan);

                var batchConcurrencySemaphore = new SemaphoreSlim(_aiBatchingOptions.MaxParallelBatches);

                var tasks = pendingBatches.Select(async batch =>
                {
                    await batchConcurrencySemaphore.WaitAsync();
                    try
                    {
                        if (!await batchService.TryMarkProcessingAsync(batch.Id))
                        {
                            Console.WriteLine($"Batch {batch.Id} ya fue tomado por otra instancia.");
                            return;
                        }

                        var pendingBatch = await batchService.GetBatchByIdAsync(batch.Id);

                        // ¿IA activa?
                        var isAiEnabled = await chatService.IsAiEnabledForChatAsync(pendingBatch.ChatId);

                        if (!isAiEnabled)
                        {
                            Console.WriteLine($"IA desactivada. Batch {pendingBatch.Id} marcado como Done.");
                            await batchService.MarkDoneAsync(pendingBatch.Id);
                            return;
                        }

                        var text = pendingBatch.AccumulatedText ?? string.Empty;
                        var images = JsonSerializer.Deserialize<List<string>>(pendingBatch.AccumulatedImages ?? "[]") ?? new();

                        Console.WriteLine($"Procesando batch {pendingBatch.Id} | Chat={pendingBatch.ChatId} | TextLen={text.Length} | Images={images.Count}");

                        await ProcessOneBatchAsync(
                            chatService,
                            aiAgentService,
                            backendApiClient,
                            chatSummaryService,
                            bannerApiService,
                            configuration,
                            hubContext,
                            pendingBatch.ChatId,
                            text,
                            images
                        );

                        await batchService.MarkDoneAsync(pendingBatch.Id);
                        Console.WriteLine($"Batch {pendingBatch.Id} completado.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Procesando batch {batch.Id}: {ex}");
                        await batchService.MarkErrorAsync(batch.Id, ex.Message);
                    }
                    finally
                    {
                        batchConcurrencySemaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AiBatchWorker loop: {ex}");
            }

            await Task.Delay(TimeSpan.FromSeconds(_aiBatchingOptions.ScanIntervalSeconds));
        }
    }

    private async Task ProcessOneBatchAsync(
        IChatService chatService,
        IaiClientService aiAgentService,
        IBackendApiClientService backendApiClient,
        ISummaryService chatSummaryService,
        IBannerWebApiService bannerApiService,
        IConfiguration configuration,
        IHubContext<ChatHub> hubContext,
        string chatId,
        string accumulatedText,
        List<string> imageUrls)
    {
        var studentEmail = await chatService.GetStudentEmailByChatIdAsync(chatId);

        var studentContext = !string.IsNullOrWhiteSpace(studentEmail)
            ? await backendApiClient.GetStudentContextByEmailAsync(studentEmail)
            : null;

        // Imágenes
        if (imageUrls?.Count > 0)
        {
            Console.WriteLine($"Enviando {imageUrls.Count} imágenes a AI | Chat={chatId}");

            var imageAnalysisResults = await aiAgentService.CallImagesAgentAsync(imageUrls, chatId);
            var validCertificateIndex = imageAnalysisResults.FindIndex(r => CertificateHelper.IsAllowed(r.Data?.Certificate));

            if (validCertificateIndex >= 0)
            {
                var imageResult = imageAnalysisResults[validCertificateIndex];
                var rawCertificateValue = imageResult.Data?.Certificate ?? "";

                if (imageResult.Data?.Escalated == "justificado")
                {
                    Console.WriteLine($"Imagen seleccionada para envío | Index={validCertificateIndex} | Certificado={rawCertificateValue} | FullName={imageResult.Data?.FullName}");

                    // Validar Identificación
                    //var iaIdentification = imageResult.Data?.Identification?.Trim();
                    //var systemIdentification = studentContext?.Identification?.Trim();

                    //if (!string.IsNullOrWhiteSpace(iaIdentification) && !string.IsNullOrWhiteSpace(systemIdentification))
                    //{
                    //    if (!string.Equals(iaIdentification, systemIdentification, StringComparison.OrdinalIgnoreCase))
                    //    {
                    //        Console.WriteLine($"[VALIDACIÓN] Identificación no coincide: IA={iaIdentification}, Sistema={systemIdentification}");
                    //        await apiClient.SendMessageToChatAsync(chatId, "No se pudo procesar la justificación porque los datos del certificado no coinciden con los del estudiante.");
                    //        return;
                    //    }
                    //}

                    // Validar Nombre
                    //var iaName = imageResult.Data?.FullName?.Trim();
                    //var systemName = studentContext?.FullName?.Trim();

                    //if (!string.IsNullOrWhiteSpace(iaName) && !string.IsNullOrWhiteSpace(systemName))
                    //{
                    //    if (!string.Equals(iaName, systemName, StringComparison.OrdinalIgnoreCase))
                    //    {
                    //        Console.WriteLine($"[VALIDACIÓN] Nombre no coincide: IA={iaName}, Sistema={systemName}");
                    //        await apiClient.SendMessageToChatAsync(chatId, "<p>No se pudo procesar la justificación porque el nombre del certificado no coincide con el del estudiante.</p>");
                    //        return;
                    //    }
                    //}

                    // Validar Fecha (hasta 5 días atrás)
                    //if (!string.IsNullOrWhiteSpace(imageResult.Data?.DateEnd))
                    //{
                    //    if (DateTime.TryParse(imageResult.Data.DateEnd, out var endDate))
                    //    {
                    //        var today = DateTime.UtcNow.Date;
                    //        var minAllowed = today.AddDays(-5);

                    //        if (endDate.Date < minAllowed || endDate.Date > today)
                    //        {
                    //            Console.WriteLine($"[VALIDACIÓN] Fecha fuera de rango permitido. DateEnd={endDate:yyyy-MM-dd}, Rango={minAllowed:yyyy-MM-dd}..{today:yyyy-MM-dd}");
                    //            await apiClient.SendMessageToChatAsync(chatId, "<p>No se pudo procesar la justificación porque la fecha del certificado no está dentro del rango permitido.</p>");
                    //            return;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        Console.WriteLine($"[VALIDACIÓN] No se pudo parsear la fecha devuelta por la IA: '{imageResult.Data.DateEnd}'");
                    //        await apiClient.SendMessageToChatAsync(chatId, "<p>No se pudo procesar la justificación porque el certificado no contiene una fecha válida.</p>");
                    //        return;
                    //    }
                    //}
                    //else
                    //{
                    //    Console.WriteLine("[VALIDACIÓN] No se detectó fecha de fin en el certificado.");
                    //    await apiClient.SendMessageToChatAsync(chatId, "<p>No se pudo procesar la justificación porque el certificado no contiene una fecha válida.</p>");
                    //    return;
                    //}

                    // Asegurar que tenemos correo del estudiante
                    if (string.IsNullOrWhiteSpace(studentEmail))
                    {
                        Console.WriteLine("[WARN] No se encontró email del estudiante para esta conversación. Se omite BannerWebApi.");
                    }
                    else
                    {
                        // Console.WriteLine($"Correo estudiante para justificación: {studentEmail}");

                        // Armar request para la imagen
                        var request = new CreateJustificationRequest
                        {
                            ChatId = chatId,
                            StudentEmail = studentEmail,
                            CertificateType = CertificateHelper.Normalize(rawCertificateValue),
                            FullName = imageResult.Data?.FullName?.Trim(),
                            Identification = imageResult.Data?.Identification?.Trim(),
                            DateInit = imageResult.Data?.DateInit,
                            DateEnd = imageResult.Data?.DateEnd,
                            EvidenceImageUrl = imageUrls[validCertificateIndex],
                            Analysis = imageResult.Data?.Analysis,
                            Summary = imageResult.Data?.Summary,
                            Source = "AI-Image"
                        };

                        try
                        {
                            var startFlowDto = new StudentJustificationRequest
                            {
                                BannerId = studentContext?.BannerId,
                                Email = studentEmail,
                                Comment = "Justificación iniciada automáticamente por IA"
                            };

                            var startFlowResponse = await bannerApiService.StartStudentJustificationFlowAsync(startFlowDto);

                            // Console.WriteLine(startFlowResponse.ResponseCode);
                            // Console.WriteLine(startFlowResponse.ResponseMessage);
                            // Console.WriteLine(startFlowResponse.Content);

                            if (startFlowResponse?.ResponseCode == 0)
                            {
                                var toEmail = configuration["EmailNotification:To"];
                                // Console.WriteLine($"To: {toEmail}");

                                var subjectTemplate = configuration["EmailNotification:SubjectJustification"]
                                                  ?? "Solicitud de Justificación de Faltas - {StudentFullName}";
                                var subject = subjectTemplate.Replace("{StudentFullName}",
                                                 string.IsNullOrWhiteSpace(studentContext?.FullName)
                                                    ? (imageResult.Data?.FullName ?? studentEmail)
                                                    : studentContext.FullName);

                                // Console.WriteLine($"Subject: {subject}");

                                var bodyBase = configuration["EmailNotification:BodyJustification"]
                                           ?? "Inicio del proceso de justificación de faltas.";

                                var htmlBody = HtmlUtils.BuildJustificationEmailHtml(
                                    studentEmail: studentEmail,
                                    bannerId: studentContext?.BannerId,
                                    career: studentContext?.Career,
                                    identification: studentContext?.Identification,
                                    studentFullName: string.IsNullOrWhiteSpace(studentContext?.FullName) ? imageResult.Data?.FullName : studentContext?.FullName,
                                    certificateType: CertificateHelper.Normalize(rawCertificateValue),
                                    dateInit: imageResult.Data?.DateInit,
                                    dateEnd: imageResult.Data?.DateEnd,
                                    summary: imageResult.Data?.Summary,
                                    analysis: imageResult.Data?.Analysis
                                );

                                // Console.WriteLine($"Body: {htmlBody}");

                                var emailSent = await backendApiClient.SendEmailWithAttachmentAsync(
                                    toEmail,
                                    subject,
                                    htmlBody,
                                    imageUrls[validCertificateIndex]
                                );

                                if (emailSent)
                                    Console.WriteLine($"Correo de justificación enviado correctamente a {studentEmail}");
                                else
                                    Console.WriteLine($"[WARN] Falló el envío de correo a {studentEmail}");
                            }
                            else
                            {
                                Console.WriteLine("No se pudo enviar el correo electrónico al área de Gestión Estudiantil.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[EX] Error al llamar BannerWebApi: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Imagen no válida para iniciar proceso de justificación.");
                }
            }
            else
            {
                Console.WriteLine("No se encontró ninguna imagen con certificado válido en este batch.");
            }
        }

        var agentRequest = new AgentRequest
        {
            SessionId = chatId,
            Prompt = accumulatedText,
            MentorGender = "",
            Email = studentEmail ?? "",
            FullName = studentContext?.FullName ?? "",
            Nickname = studentContext?.FavoriteName ?? "",
            Career = studentContext?.Career ?? "",
            IdCard = studentContext?.Identification ?? "",
            StudentGender = studentContext?.Gender ?? ""
        };

        var aiResponseMessage = await ProcessAiMessageAsync(aiAgentService, agentRequest);

        if (string.IsNullOrWhiteSpace(aiResponseMessage))
        {
            Console.WriteLine("AI devolvió respuesta vacía.");
            return;
        }

        // Escalado IA
        if (aiResponseMessage.Contains("--mentor--"))
        {
            Console.WriteLine("Marcador '--mentor--' detectado. Desactivando AI y enviando respuesta limpia.");

            var result = await backendApiClient.UpdateAISettingsAsync(chatId, aiState: false, reason: "AI");
            if (!result) 
                Console.WriteLine("[WARN] No se pudo desactivar AI.");

            try
            {
                await hubContext
                    .Clients
                    .Group($"chat:{chatId}")
                    .SendAsync("ReceiveMessage", new
                {
                    ChatId = chatId,
                    MessageId = (int?)null, // No hay mensaje
                    SenderRole = "system",
                    Content = (string?)null, // No hay contenido
                    ContentType = "system-event",
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    AiEnabled = false,
                    Attachments = new List<object>()
                });

                Console.WriteLine($"[SignalR] Estado de IA desactivado notificado | Chat={chatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Error notificando estado IA: {ex.Message}");
            }

            var cleanResponse = aiResponseMessage.Replace("<p>--mentor--</p>", "").Replace("--mentor--", "").Trim();

            var summary = await aiAgentService.CallSummaryAgentAsync(chatId);

            if (summary != null)
            {
                await chatSummaryService.SaveSummaryAsync(chatId, "AI", summary);
                Console.WriteLine("Resumen AI guardado.");
            }

            if (!string.IsNullOrWhiteSpace(cleanResponse))
                await backendApiClient.SendMessageToChatAsync(chatId, cleanResponse);
        }
        else
        {
            await backendApiClient.SendMessageToChatAsync(chatId, aiResponseMessage);
        }

        await chatService.UpdateLastAiBatchDateAsync(chatId);

        Console.WriteLine($"LastAiBatchAt actualizado | Chat={chatId}");
    }

    private static async Task<string?> ProcessAiMessageAsync(
        IaiClientService agentClientService,
        AgentRequest request)
    {
        // Validación indispensable
        if (string.IsNullOrWhiteSpace(request?.SessionId))
        {
            Console.WriteLine("[AI Worker] AgentRequest inválido.");
            return null;
        }

        var result = await agentClientService
            .CallTextAgentAsync(request)
            .ConfigureAwait(false);

        if (result?.Success == true)
        {
            Console.WriteLine($"[AI Worker] Respuesta del agente: {result.Data.Response}");
            return result.Data.Response;
        }

        Console.WriteLine("[AI Worker][ERROR] No se obtuvo respuesta válida del agente.");
        return null;
    }
}
