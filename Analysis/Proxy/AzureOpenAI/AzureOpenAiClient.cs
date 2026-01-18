// ReSharper disable InconsistentNaming
namespace Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;

using System.Text;
using System.Text.Json;
using AzO = Azure.AI.OpenAI;          // puente Azure
using Chat = OpenAI.Chat;             // tipos de chat v2
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public interface IGenerativeClient
{
    Task<AiAnalysisResult> AnalyzeAsync(ChatTranscript transcript, CancellationToken ct = default);
}

public sealed class AzureOpenAiClient : IGenerativeClient
{
    private readonly Chat.ChatClient _chat;
    private readonly ILogger<AzureOpenAiClient> _log;

    public AzureOpenAiClient(
     Azure.AI.OpenAI.AzureOpenAIClient aoaiClient,
     IOptions<AzureOpenAIOptions> opts,
     ILogger<AzureOpenAiClient> log)
    {
        if (aoaiClient is null) throw new ArgumentNullException(nameof(aoaiClient));
        var o = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
        if (string.IsNullOrWhiteSpace(o.Deployment))
            throw new ArgumentException("AzureOpenAI.Deployment vacío.", nameof(opts));

        _chat = aoaiClient.GetChatClient(o.Deployment);
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<AiAnalysisResult> AnalyzeAsync(ChatTranscript transcript, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(transcript);

        // v2.0.0: los mensajes van como argumento; Options NO tiene propiedad Messages
        var messages = new List<Chat.ChatMessage>
        {
            new Chat.SystemChatMessage(
                 "Eres un auditor estricto y consistente de interacciones educativas. " +
                 "Evalúa con tendencia al punto medio salvo que la evidencia sea muy fuerte. " +
                 "Devuelve ÚNICAMENTE un objeto JSON compacto que siga exactamente el esquema proporcionado. Sin markdown ni comentarios adicionales."),
            new Chat.UserChatMessage(prompt)
        };

        var options = new Chat.ChatCompletionOptions
        {
            Temperature = 0.3f,
            TopP = 0.9f,
            MaxOutputTokenCount = 800
        };

        // v2.0.0: CompleteAsync(mensajes, options, ct)
        var response = await _chat.CompleteChatAsync(messages, options, ct);
        Chat.ChatCompletion completion = response.Value;

        // En 2.0.0 el contenido viene en partes con .Text
        string text = string.Concat(completion.Content.Select(p => p.Text));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Azure OpenAI devolvió contenido vacío.");

        text = TrimCodeFences(text);
        return ParseResult(text);
    }

    // ----------------- helpers (igual que los tuyos) -----------------

    private static string BuildPrompt(ChatTranscript t)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Evalúa la interacción semanal entre Estudiante, Mentor y la IA.");
        sb.AppendLine("Reglas CRÍTICAS:");
        sb.AppendLine("- SIEMPRE devuelve un JSON válido completo, incluso si faltan datos.");
        sb.AppendLine("- NUNCA devuelvas texto plano como respuesta.");
        sb.AppendLine("- Si un dato no puede inferirse, usa estos valores por defecto:");
        sb.AppendLine("  * Para números: 0");
        sb.AppendLine("  * Para strings: \"No disponible\"");
        sb.AppendLine("  * Para sentiment: \"Neutro\"");
        sb.AppendLine("- El JSON debe ser parseable sin errores.");
        sb.AppendLine("- NO incluyas markdown (```json) ni comentarios.");
        sb.AppendLine();
        sb.AppendLine(@"JSON schema:
            {
              ""misunderstood_pct"": number,
              ""empathy_ai"": number,
              ""empathy_mentor"": number,
              ""sentiment_student_start"" string: Emoción predominante del estudiante AL INICIO de la conversación. Debe ser exactamente una de estas 9 opciones: ""Ansioso"", ""Confundido"", ""Frustrado"", ""Neutro"", ""Motivado"", ""Tranquilo"", ""Alegre"", ""Triste"", ""Enojado""
              ""sentiment_student_end"" string: Emoción predominante del estudiante AL FINAL de la conversación. Debe ser exactamente una de estas 9 opciones: ""Ansioso"", ""Confundido"", ""Frustrado"", ""Neutro"", ""Motivado"", ""Tranquilo"", ""Alegre"", ""Triste"", ""Enojado""
              ""emotion_avg"": number,
              ""warmth_ai"": number,
              ""warmth_mentor"": number,
              ""overall_comment"": string,
              ""satisfiedUser"": string (Escribe true si la petición fue resuelta, false si no),
              ""issue"": string (tema principal: ""Pregunta frecuente"" o ""Justificación de inasistencia"")
            }");
        sb.AppendLine();
        sb.AppendLine("Transcripción (en orden cronológico):");
        foreach (var m in t.Turns.OrderBy(x => x.CreatedAtUtc))
        {
            var role = $"{m.SenderRole}{(m.IAEnabled ? " (IA ON)" : "")}";
            sb.Append('[').Append(m.CreatedAtUtc.ToString("O")).Append("] ")
              .Append(role).Append(": ").AppendLine(m.Text);
        }
        return sb.ToString();
    }

    private static string TrimCodeFences(string s)
    {
        var trimmed = s.Trim();
        if (trimmed.StartsWith("```"))
        {
            int firstNl = trimmed.IndexOf('\n');
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNl >= 0 && lastFence > firstNl)
                trimmed = trimmed[(firstNl + 1)..lastFence].Trim();
        }
        return trimmed;
    }

    private static AiAnalysisResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        decimal GetNum(string name, decimal def = 0m)
        {
            if (!root.TryGetProperty(name, out var p)) return def;
            return p.ValueKind switch
            {
                JsonValueKind.Number => p.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(p.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def,
                _ => def
            };
        }

        string GetStr(string name) =>
            root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

        static decimal Clamp(decimal v, decimal min, decimal max) => v < min ? min : (v > max ? max : v);

        return new AiAnalysisResult(
            MisunderstoodPct: Clamp(GetNum("misunderstood_pct"), 0, 100),
            EmpathyAi: Clamp(GetNum("empathy_ai"), 1, 10),
            EmpathyMentor: Clamp(GetNum("empathy_mentor"), 1, 10),
            SentimentStudentStart: GetStr("sentiment_student_start"),
            SentimentStudentEnd: GetStr("sentiment_student_end"),
            EmotionAvg: Clamp(GetNum("emotion_avg"), 1, 10),
            WarmthAi: Clamp(GetNum("warmth_ai"), 1, 10),
            WarmthMentor: Clamp(GetNum("warmth_mentor"), 1, 10),
            OverallComment: GetStr("overall_comment"),
            SatisfiedUser: GetStr("satisfiedUser"),
            Issue: GetStr("issue")
        );
    }
}
