using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common;

public static class EnvironmentHelper
{
    private static string? _baseUrl;

    /// <summary>
    /// Inicializa la URL base del backend dependiendo del ambiente.
    /// Este método debe llamarse durante la configuración del sistema (Startup/Program.cs).
    /// </summary>
    public static void Initialize(IConfiguration configuration, IHostEnvironment environment)
    {
        // Ambiente local donde el backend suele exponerse directamente.
        if (environment.IsDevelopment())
        {
            // Se espera un valor explícito Backend:Endpoint en appsettings.Development.json
            var backendEndpoint = configuration["Backend:Endpoint"];

            // Se registra el endpoint como base URL del backend
            _baseUrl = !string.IsNullOrWhiteSpace(backendEndpoint)
                ? backendEndpoint.TrimEnd('/')
                : throw new ArgumentNullException(nameof(backendEndpoint),
                    "Backend:Endpoint no configurado en entorno de desarrollo");
        }
        // Ambientes de despliegue (Producción o Staging)
        else if (environment.IsProduction() || environment.IsStaging() || environment.IsEnvironment("Production"))
        {
            // En producción, el backend está detrás de la ruta '/backend' del frontend
            var frontendEndpoint = configuration["Frontend:Endpoint"];

            // Se construye la URL pública combinada para direccionar al backend
            _baseUrl = !string.IsNullOrWhiteSpace(frontendEndpoint)
                ? $"{frontendEndpoint.TrimEnd('/')}/backend"
                : throw new ArgumentNullException("Frontend:Endpoint no configurado");
        }
        else
        {
            throw new InvalidOperationException(
                $"Ambiente no soportado: {environment.EnvironmentName}. " +
                "Configurar explícitamente la resolución de base URL.");
        }
    }

    /// <summary>
    /// Obtiene la URL base del backend sin requerir parámetros adicionales.
    /// </summary>
    public static string GetBackendBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new InvalidOperationException(
                "EnvironmentHelper no inicializado. " +
                "Asegúrate de llamar EnvironmentHelper.Initialize(...) al configurar la aplicación.");

        return _baseUrl;
    }
}
