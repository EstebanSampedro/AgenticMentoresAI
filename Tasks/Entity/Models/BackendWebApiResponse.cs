using Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class BackendWebApiResponse<T>
{
    public ResponseTypeCodeDto ResponseCode { get; set; }
    public string? ResponseMessage { get; set; }
    public T? ResponseData { get; set; }
}
