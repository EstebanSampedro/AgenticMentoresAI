namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public sealed class ServiceResult<T>
{
    public bool Success { get; init; }
    public EnumSummaryResult Code { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Code = EnumSummaryResult.None, Message = message, Data = data };

    public static ServiceResult<T> Fail(EnumSummaryResult code, string? message = null, T? data = default) =>
        new() { Success = false, Code = code, Message = message, Data = data };
}

public enum EnumSummaryResult
{
    None = 0,
    InvalidChatId = 1,
    ChatNotFound = 2,
    NoActiveConversation = 3,
    NoMessagesInConversation = 4,
    AiCallFailed = 5,
    AiBadPayload = 6,
    PersistFailed = 7,
    InvalidTimeZone = 8,
    DailySummaryAlreadyExists = 1001,
    MultipleDailySummariesDetected = 1002,
    UnknownError = 99
}
