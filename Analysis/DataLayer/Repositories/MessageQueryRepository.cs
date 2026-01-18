using System.Data;
using System.Data.SqlClient;
using Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Academikus.AnalysisMentoresVerdes.Utility.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Academikus.AnalysisMentoresVerdes.Data.Repositories;

public sealed class MessageQueryRepository : IMessageQueryRepository
{
    private readonly string _connStr;
    private readonly ILogger<MessageQueryRepository> _log;

    public MessageQueryRepository(IConfiguration cfg, ILogger<MessageQueryRepository> log)
    {
        _connStr = cfg.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");
        _log = log;
    }

    public async Task<IReadOnlyList<ChatTranscript>> GetTranscriptsAsync(WeeklyWindow window, CancellationToken ct)
    {
        using var con = new SqlConnection(_connStr);
        await con.OpenAsync(ct);

        // SQL: mensajes de la semana + estado IA por intervalo
        const string sql = @"
        DECLARE @WeekStartUtc DATETIME2(0) = @p_WeekStartUtc;
        DECLARE @WeekEndUtc   DATETIME2(0) = @p_WeekEndUtc;

        WITH IAIntervals AS (
            SELECT ChatId, IaState, CreatedAt
            FROM dbo.ChatIALog
            WHERE CreatedAt < @WeekEndUtc
        ),
        Intervals AS (
            SELECT 
                ChatId,
                IaState,
                FromAt = CreatedAt,
                ToAt   = LEAD(CreatedAt,1,@WeekEndUtc) OVER (PARTITION BY ChatId ORDER BY CreatedAt)
            FROM IAIntervals
        ),
        Msg AS (
            SELECT 
                m.Id              AS MessageId,
                c.ChatId,
                ch.MentorId,
                ch.StudentId,
                m.SenderRole,
                m.MessageContent,
                m.CreatedAt
            FROM dbo.Message m
            JOIN dbo.Conversation c ON c.Id = m.ConversationId
            JOIN dbo.Chat ch ON ch.Id = c.ChatId
            WHERE m.CreatedAt >= @WeekStartUtc AND m.CreatedAt < @WeekEndUtc
        )
        SELECT 
            Msg.ChatId,
            Msg.MentorId,
            Msg.StudentId,
            Msg.MessageId,
            Msg.SenderRole,
            Msg.MessageContent,
            Msg.CreatedAt,
            IA.IaState
        FROM Msg
        LEFT JOIN Intervals IA
          ON IA.ChatId = Msg.ChatId
         AND Msg.CreatedAt >= IA.FromAt AND Msg.CreatedAt < IA.ToAt
        ORDER BY Msg.ChatId, Msg.CreatedAt;";

        using var cmd = new SqlCommand(sql, con)
        {
            CommandType = CommandType.Text
        };
        cmd.Parameters.Add(new SqlParameter("@p_WeekStartUtc", SqlDbType.DateTime2) { Value = window.WeekStartUtc });
        cmd.Parameters.Add(new SqlParameter("@p_WeekEndUtc", SqlDbType.DateTime2) { Value = window.WeekEndUtc });

        var map = new Dictionary<long, (long? MentorId, long? StudentId, List<ChatMessageTurn> Turns)>();

        using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var chatId = rd.GetInt64(rd.GetOrdinal("ChatId"));
            var mentorId = rd.IsDBNull(rd.GetOrdinal("MentorId")) ? (long?)null : rd.GetInt64(rd.GetOrdinal("MentorId"));
            var studentId = rd.IsDBNull(rd.GetOrdinal("StudentId")) ? (long?)null : rd.GetInt64(rd.GetOrdinal("StudentId"));
            var senderRole = rd.GetString(rd.GetOrdinal("SenderRole"));     // 'Student' | 'Mentor' | 'AI' (ajusta si tus valores difieren)
            var html = rd.IsDBNull(rd.GetOrdinal("MessageContent")) ? "" : rd.GetString(rd.GetOrdinal("MessageContent"));
            var createdAt = rd.GetDateTime(rd.GetOrdinal("CreatedAt"));
            var iaEnabled = !rd.IsDBNull(rd.GetOrdinal("IaState")) && rd.GetInt32(rd.GetOrdinal("IaState")) == 1;

            var clean = TextSanitizer.Anonymize(TextSanitizer.ToPlainText(html));

            if (!map.TryGetValue(chatId, out var tuple))
            {
                tuple = (mentorId, studentId, new List<ChatMessageTurn>(256));
                map[chatId] = tuple;
            }
            tuple.Turns.Add(new ChatMessageTurn(
                CreatedAtUtc: createdAt,
                SenderRole: senderRole,
                IAEnabled: iaEnabled,
                Text: clean));
        }

        var result = map.Select(kv => new ChatTranscript(
            ChatId: kv.Key,
            MentorId: kv.Value.MentorId,
            StudentId: kv.Value.StudentId,
            Turns: kv.Value.Turns.OrderBy(t => t.CreatedAtUtc).ToList()
        )).ToList();

        _log.LogInformation("Built {Count} transcripts from DB.", result.Count);
        return result;
    }
}
