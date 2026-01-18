namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

/// <summary>
/// Clase para manejar las respuestas de la Web API (genérica)
/// </summary>
public class WebApiResponseDto<T>
{
    public ResponseTypeCodeDto ResponseCode { get; init; } = ResponseTypeCodeDto.Ok;
    public string ResponseMessage { get; init; } = string.Empty;
    public T? ResponseData { get; init; }

    public static WebApiResponseDto<T> Ok(T data, string? message = null, string? user = null) => new()
    {
        ResponseCode = ResponseTypeCodeDto.Ok,
        ResponseMessage = message ?? "Datos obtenidos con éxito.",
        ResponseData = data
    };

    public static WebApiResponseDto<T> Error(string message, string? user = null, T? data = default) => new()
    {
        ResponseCode = ResponseTypeCodeDto.Error,
        ResponseMessage = message,
        ResponseData = data
    };

    public static WebApiResponseDto<T> NoData(string? message = null, string? user = null) => new()
    {
        ResponseCode = ResponseTypeCodeDto.NoData,
        ResponseMessage = message ?? "No existen datos.",
        ResponseData = default
    };
}

// Shim no genérico (compatibilidad con código que usa 'new WebApiResponseDTO()')
public class WebApiResponseDTO : WebApiResponseDto<object> { }
