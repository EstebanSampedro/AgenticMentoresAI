namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

/// <summary>Transcripción semanal por chat, ya con texto limpio/anonimizado.</summary>
public sealed record ChatTranscript(
    long ChatId,
    long? MentorId,
    long? StudentId,
    IReadOnlyList<ChatMessageTurn> Turns);

/// <summary>Mensaje individual dentro de la transcripción.</summary>
public sealed record ChatMessageTurn(
    DateTime CreatedAtUtc,
    string SenderRole,   // "Student" | "Mentor" | "AI"
    bool IAEnabled,      // si la IA estaba ON al momento del mensaje
    string Text);        // plano y anonimizado
