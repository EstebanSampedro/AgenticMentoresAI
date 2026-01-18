namespace Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;

/// <summary>
/// Clase para manejar las respuestas de la web api
/// </summary>
public class WebApiResponseDto<T>
{
    public ResponseTypeCodeDto ResponseCode { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;
    public string? User { get; set; }
    public T? ResponseData { get; set; }

    public static WebApiResponseDto<T> Ok(T data, string? msg = null, string? user = null) =>
        new() { ResponseCode = ResponseTypeCodeDto.Ok, ResponseMessage = msg ?? "Datos obtenidos con éxito.", User = user, ResponseData = data };

    public static WebApiResponseDto<T> Error(string msg, string? user = null, T? data = default) =>
        new() { ResponseCode = ResponseTypeCodeDto.Error, ResponseMessage = msg, User = user, ResponseData = data };

    public static WebApiResponseDto<T> NoData(string? msg = null, string? user = null) =>
        new() { ResponseCode = ResponseTypeCodeDto.NoData, ResponseMessage = msg ?? "No existen datos.", User = user };
}

public class WebApiResponseDTO : WebApiResponseDto<object> { }