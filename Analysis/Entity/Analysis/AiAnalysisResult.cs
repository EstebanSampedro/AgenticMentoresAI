namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

/// <summary>Resultado devuelto por la IA para una transcripción semanal.</summary>
public sealed record AiAnalysisResult(
    decimal MisunderstoodPct,        // 0..100
    decimal EmpathyAi,               // 1..10
    decimal EmpathyMentor,           // 1..10
    string SentimentStudentStart,  // texto del sentimiento con el que inició la conversación 
    string SentimentStudentEnd,    // texto del sentimiento con el que terminó la conversación
    decimal EmotionAvg,              // 1..10
    decimal WarmthAi,                // 1..10
    decimal WarmthMentor,            // 1..10
    string OverallComment,          // comentario Final 
    string SatisfiedUser,       // Si se cumplió  el pedido 
    string Issue);              // Temática de conversación 
