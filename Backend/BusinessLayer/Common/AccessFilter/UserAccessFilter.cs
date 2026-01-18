using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;

/// <summary>
/// Filtro de autorización que valida el acceso de usuarios autenticados,
/// verificando correo institucional, rol de Mentor y estado activo.
/// Permite tokens de aplicación cuando el AppId coincide con la configuración.
/// </summary>
/// <param name="context">Contexto de autorización que contiene información del usuario y la petición.</param>
public class UserAccessFilter : IAsyncAuthorizationFilter
{
    private readonly DBContext _db;
    private readonly IConfiguration _configuration;

    public UserAccessFilter(DBContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Se obtiene el usuario autenticado desde el contexto HTTP
        var user = context.HttpContext.User;

        // Se valida que el token esté autenticado
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            Console.WriteLine("[AuthFilter] Token no válido o no autenticado.");

            context.Result = new ObjectResult(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Token no válido o no autenticado.",
                ResponseData = null
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };

            return;
        }

        // Se extraen los claims relevantes para obtener el correo o AppId
        var appId = user.FindFirst("appid")?.Value;
        var email = user.FindFirst("preferred_username")?.Value
                  ?? user.FindFirst("upn")?.Value
                  ?? user.FindFirst("unique_name")?.Value
                  ?? user.FindFirst("email")?.Value
                  ?? user.Identity?.Name;

        // Se valida si es un token de aplicación autorizado
        if (string.IsNullOrWhiteSpace(email))
        {
            var authorizedAppId = _configuration["AzureAd:ClientId"];

            if (!string.IsNullOrWhiteSpace(appId) &&
                appId == authorizedAppId)
            {
                //Console.ForegroundColor = ConsoleColor.Yellow;
                //Console.WriteLine($"[AuthFilter] Token de aplicación detectado (AppId={appId}). Acceso permitido sin validar correo.");
                //Console.ResetColor();
                return;
            }

            Console.WriteLine("[AuthFilter] No se pudo extraer correo ni se detectó AppId autorizado.");

            context.Result = new ObjectResult(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "No se pudo extraer correo ni se detectó AppId autorizado.",
                ResponseData = null
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Se normaliza el correo antes de las validaciones
        email = email.Trim().ToLowerInvariant();

        // Se valida el dominio institucional
        if (!email.EndsWith("@udla.edu.ec", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[AuthFilter] Acceso denegado: el usuario {email} no pertenece al dominio udla.edu.ec.");

            context.Result = new ObjectResult(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El usuario no pertenece al dominio udla.edu.ec.",
                ResponseData = null
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        try
        {
            // Se verifica que el usuario exista y tenga rol Mentor activo
            var dbUser = await _db.UserTables
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.UserRole == "Mentor" &&
                    u.UserState == "Activo");

            if (dbUser == null)
            {
                Console.WriteLine($"[AuthFilter] Acceso denegado: el usuario {email} no está activo o no tiene rol de Mentor.");

                context.Result = new ObjectResult(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.Error,
                    ResponseMessage = "El usuario no está activo o no tiene rol de Mentor.",
                    ResponseData = null
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthFilter] Error validando acceso de usuario {email}. Detalle: {ex}");

            context.Result = new ObjectResult(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno validando acceso del usuario.",
                ResponseData = null
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
