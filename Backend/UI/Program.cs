#region using
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.KeyVault;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.AI;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Attachments;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.BannerWebApi;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Cases;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.ClientLogs;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Email;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Images;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Security;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Semester;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Subscriptions;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.Command.Core;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.DataProtectionKeys.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.LoggerDB;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Common;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Academikus.AgenteInteligenteMentoresWebApi.Utility.WebApi;
using Academikus.AgenteInteligenteMentoresWebApi.WebApi.Middleware;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using WebApiTemplate.AuthorizeDLL;
using WebApiTemplate.AuthorizeDLL.DatabaseConnection;
#endregion

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.json", optional: false)
    .AddCommandLine(args)
    .AddEnvironmentVariables()
    .Build();

// Credenciales para autenticarse en Azure AD (bootstrap)
var keyVaultUrl = builder.Configuration["KeyVault:Url"];

var tenantId = builder.Configuration["AZURE_TENANT_ID"];
var clientId = builder.Configuration["AZURE_CLIENT_ID"];
var clientSecret = builder.Configuration["AZURE_CLIENT_SECRET"];

var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var secretClient = new SecretClient(new Uri(keyVaultUrl), credential);

builder.Configuration.Sources.Add(
    new KeyVaultMappedJsonSecretsSource(
        secretClient,
        builder.Configuration.GetSection("Secrets:Map")
    )
);

var healthCheck = builder.Configuration["HealthCheck:KeyVault"];
Console.WriteLine($"[KeyVault] Health Check: {healthCheck}");

var configuration = builder.Configuration;

builder.Services.Configure<AppSetting>(
    configuration.GetSection("AppSettings"));
builder.Services.Configure<ConnectionString>(
    configuration.GetSection("ConnectionStrings"));
builder.Services.Configure<AzureAdOptions>(
    configuration.GetSection("AzureAd"));
builder.Services.Configure<SalesforceOptions>(
    configuration.GetSection("Salesforce"));
builder.Services.Configure<BannerWebApiOptions>(
    configuration.GetSection("BannerWebApi"));
builder.Services.Configure<GraphOptions>(
    builder.Configuration.GetSection("Graph"));
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));
builder.Services.Configure<ServiceAccountOptions>(
    configuration.GetSection("ServiceAccount"));

// CONEXIÓN A BD
string connectionString = configuration["ConnectionStrings:DefaultConnection"];

// Definir la política CORS
string corsPolicy = "AllowAll";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicy, policy =>
    {
        policy
        .WithOrigins(configuration["Frontend:Endpoint"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        // options.Filters.Add<UserAccessFilter>();
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

//Log4net Declaration
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Academikus.AgenteInteligenteMentoresWebApi.WebApi", Version = "v1" });
    //c.DescribeAllParametersInCamelCase();
    var filePath = Path.Combine(System.AppContext.BaseDirectory, "Academikus.AgenteInteligenteMentoresWebApi.WebApi.xml");
    c.IncludeXmlComments(filePath);
    // Agrega en swagger la seguridad de tipo Bearer para poder insertar el token y consumir endpoints protegidos UdlaWebApiAuthorize
    // TODO: Agregar a plantilla
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert Bearer token into field",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type=ReferenceType.SecurityScheme,
                                Id="Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Description = "Basic auth added to authorization header",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Scheme = "basic",
        Type = SecuritySchemeType.Http
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Basic" }
                    },
                    new List<string>()
                }
            });
});

// REGISTRAR SERVICIOS
builder.Services.AddHttpClient("salesforce-oauth", c =>
{
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddHttpClient<ISalesforceApiClient, SalesforceApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Salesforce:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<UserExcelSyncWorker>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IBannerWebApiService, BannerWebApiService>();
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddSingleton<MicrosoftGraphAuthService>();
builder.Services.AddScoped<MentorService>();
builder.Services.AddScoped<IMentorService, MentorService>();
builder.Services.AddScoped<IMentorLookupService, MentorLookupService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<ISemesterService, SemesterService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<UserAccessFilter>();
builder.Services.AddSingleton<ISalesforceTokenProvider, SalesforceTokenProvider>();
builder.Services.AddScoped<IaiClientService, AiClientService>();
builder.Services.AddScoped<IClientLogSink, ClientLogSink>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = true;
});

builder.Services.AddSingleton<GraphServiceClient>(_ =>
{
    var tenantId = configuration["AzureAd:TenantId"];
    var clientId = configuration["AzureAd:ClientId"];
    var clientSecret = configuration["AzureAd:ClientSecret"];

    var credential = new ClientSecretCredential(
        tenantId,
        clientId,
        clientSecret
    );

    return new GraphServiceClient(credential);
});

builder.Services.AddSingleton<SecretClient>(provider =>
{
    var keyVaultUrl = configuration["KeyVault:Url"];

    var tenantId = configuration["AzureAd:TenantId"];
    var clientId = configuration["AzureAd:ClientId"];
    var clientSecret = configuration["AzureAd:ClientSecret"];

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    return new SecretClient(new Uri(keyVaultUrl), credential);
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = false;
});

builder.Services.AddSingleton<ITokenStoreService, KeyVaultTokenStoreService>();
// FIN REGISTRAR SERVICIOS

builder.Services.Configure<ApiBehaviorOptions>(options
    => options.SuppressModelStateInvalidFilter = true);

// Agregar contexto de base de datos para WebApiAuthorize
builder.Services.AddDbContext<WebApiDBEntities>(options =>
{
    options.UseSqlServer(configuration.GetConnectionString("WebApiDBEntities"),
        sqlServerOptionsAction: sqlOptions => //Validar si es necesario agregar el retry
        {
            sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
        });

});

// Esquema de Autenticación
// Nota: Los tokens generados por la aplicación de tareas (Client Credentials)
// no contienen usuario (upn/email). Por esto se detectan con el claim "appid" y
// se crea un ClaimsPrincipal manual ("AppIdentity") para permitir su uso en APIs internas.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        // Cargar todos los valores de AzureAd (Authority, TenantId, etc.)
        configuration.Bind("AzureAd", options);

        var apiUri = configuration["AzureAd:ApiUri"]?.Trim();
        if (string.IsNullOrEmpty(apiUri))
        {
            throw new InvalidOperationException("AzureAd:ApiUri no está configurado en appsettings.json o variables de entorno.");
        }

        // Audiencia principal
        options.TokenValidationParameters.ValidAudience = apiUri;

        // Emisor esperado
        options.TokenValidationParameters.ValidIssuer =
            $"https://sts.windows.net/{configuration["AzureAd:TenantId"]}/";

        // Validación de tiempo de vida y desfase horario (cliente-servidor) del token
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);

        // Agregar eventos de depuración
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!context.Request.Headers.ContainsKey("Authorization"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("No se recibió el encabezado Authorization.");
                    Console.ResetColor();
                }
                else if (!context.Request.Headers["Authorization"].ToString()
                            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("El encabezado Authorization no tiene el formato esperado 'Bearer <token>'.");
                    Console.ResetColor();
                }
                //else
                //{
                //    var token = context.Request.Headers["Authorization"].ToString();
                //    Console.ForegroundColor = ConsoleColor.Blue;
                //    Console.WriteLine($"Token recibido: {token}");
                //    Console.ResetColor();
                //}

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("[JwtBearer] OnChallenge ejecutado:");
                Console.WriteLine($"Error: {context.Error}");
                Console.WriteLine($"Descripción: {context.ErrorDescription}");
                Console.WriteLine($"URI: {context.ErrorUri}");
                Console.WriteLine($"Principal: {(context.HttpContext.User?.Identity?.IsAuthenticated == true ? "Sí" : "No")}");
                Console.ResetColor();
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error de autenticación:");
                Console.WriteLine(context.Exception.ToString());

                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var tokenString = authHeader.ToString()
                        .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var token = handler.ReadJwtToken(tokenString);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("=== HEADER ===");
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(token.Header, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        Console.WriteLine("=== PAYLOAD ===");
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(token.Payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error al decodificar token: {ex.Message}");
                    }
                }

                Console.ResetColor();
                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                var jwt = context.SecurityToken as JwtSecurityToken;
                var appId = jwt?.Claims.FirstOrDefault(c => c.Type == "appid")?.Value;

                // Detectar token de aplicación
                if (!string.IsNullOrEmpty(appId))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[JwtBearer] Token de aplicación detectado (AppId={appId}). Forzando autenticación...");
                    Console.ResetColor();

                    var identity = new ClaimsIdentity(
                        authenticationType: JwtBearerDefaults.AuthenticationScheme
                    );
                    identity.AddClaim(new Claim(ClaimTypes.Name, "AppIdentity"));
                    identity.AddClaim(new Claim("appid", appId));

                    // Forzar el estado autenticado explícitamente
                    identity.AddClaim(new Claim("authenticated", "true"));
                    typeof(ClaimsIdentity)
                        .GetProperty("IsAuthenticated")?
                        .SetValue(identity, true);

                    context.Principal = new ClaimsPrincipal(identity);
                    context.Success();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[JwtBearer] Principal autenticado correctamente (AppIdentity).");
                    Console.ResetColor();
                }
                else
                {
                    // Usuario normal
                    context.Success();
                }

                return Task.CompletedTask;
            }
        };
    },
    options => configuration.Bind("AzureAd", options));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AppOrUser", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            return user.HasClaim(c => c.Type == "appid") // token de aplicación
                || user.HasClaim(c => c.Type == "upn")   // usuario (Azure)
                || user.HasClaim(c => c.Type == "preferred_username")
                || user.HasClaim(c => c.Type == "email");
        });
    });
});

// IDistributedCache en SQL (para MSAL Token Cache)
builder.Services.AddDistributedSqlServerCache(o =>
{
    o.ConnectionString = connectionString;

    o.SchemaName = configuration["SqlCache:Schema"] ?? "dbo";
    o.TableName = configuration["SqlCache:Table"] ?? "TokenCache";
});

// Data Protection Keys en SQL (necesarias para descifrar el cache tras reinicios)
builder.Services.AddDbContext<DataProtectionKeyContext>(opt =>
    opt.UseSqlServer(connectionString));

// Persistir las claves de Data Protection en SQL (tabla dbo.DataProtectionKeys)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DataProtectionKeyContext>()
    .SetApplicationName("Academikus.AgenteInteligenteMentoresWebApi.WebApi");

// MSAL usará IDistributedCache automáticamente
builder.Services.AddDistributedTokenCaches();

// MSAL Confidential Client + wiring de caches
builder.Services.AddSingleton(sp =>
{
    var cca = ConfidentialClientApplicationBuilder
        .Create(configuration["AzureAd:ClientId"])
        .WithClientSecret(configuration["AzureAd:ClientSecret"])
        .WithAuthority($"{configuration["AzureAd:Instance"]}{configuration["AzureAd:TenantId"]}")
        .Build();

    var cacheProvider = sp.GetRequiredService<IMsalTokenCacheProvider>();

    cacheProvider.Initialize(cca.AppTokenCache);
    cacheProvider.Initialize(cca.UserTokenCache);

    return cca;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429; // Status que se devuelve al exceder el límite

    options.OnRejected = (context, token) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var endpoint = context.HttpContext.Request.Path;

        logger.LogWarning("Solicitud rechazada por Rate Limiting en {Endpoint}. Razón: {Reason}",
            endpoint,
            context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ?
                $"RetryAfter = {retry}" : "Límite alcanzado");

        context.HttpContext.Response.Headers["Retry-After"] = "60"; // segundos

        return ValueTask.CompletedTask;
    };

    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 10; // Para pruebas
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 1; // Para pruebas
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

//Dependency Injection
builder.Services.AddScoped<IWebApiInvoker, WebApiInvoker>();
builder.Services.AddScoped<IWebApiInvokerConecte, WebApiInvokerConecte>();
builder.Services.AddScoped<ICommandExecutor, CommandExecutor>();
builder.Services.AddScoped<WebApiBO, WebApiBO>();
builder.Services.AddScoped<ILoggerDbRepository, LoggerDbRepository>();
builder.Services.AddSingleton<StaticAccessTokenProvider>();

// Crypto para proteger/desproteger sessionKey
builder.Services.AddSingleton<LroCrypto>();

// Repositorio EF (si lo tienes en Business)
builder.Services.AddScoped<ILroSessionService, LroSessionService>();

// Orquestador OBO
builder.Services.AddScoped<IOboSessionOrchestrator, OboSessionOrchestrator>();

//Agregar secciones de configuración
builder.Services.AddOptions();

builder.Services.AddDbContext<DBContext>(options =>
    options.UseSqlServer(connectionString));
// FIN CONEXIÓN A BD

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (error == null)
            return;

        // Determina el código de estado según tipo de excepción
        var httpStatus = error switch
        {
            HttpRequestException => StatusCodes.Status502BadGateway,  // error externo
            KeyNotFoundException => StatusCodes.Status404NotFound,     // recurso no encontrado
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized, // sin autorización
            _ => StatusCodes.Status500InternalServerError              // genérico
        };

        // Determina código lógico (ResponseTypeCodeDTO)
        var responseCode = error switch
        {
            KeyNotFoundException => ResponseTypeCodeDto.NoData,
            _ => ResponseTypeCodeDto.Error
        };

        // Mensaje según el tipo de error
        var responseMessage = httpStatus switch
        {
            StatusCodes.Status404NotFound => "Recurso no encontrado.",
            StatusCodes.Status401Unauthorized => "Acceso no autorizado.",
            StatusCodes.Status502BadGateway => "Error al comunicarse con un servicio externo.",
            _ => "Ocurrió un error interno en el servidor."
        };

        // Cambia el color de texto según gravedad
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("─────────────────────────────");
        Console.WriteLine($"❌ Excepción no controlada: {error.GetType().Name}");
        Console.WriteLine($"📄 Mensaje: {error.Message}");
        Console.WriteLine($"📍 Ruta: {context.Request?.Path}");
        Console.WriteLine($"🕓 Fecha: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"🔍 StackTrace:\n{error.StackTrace}");
        Console.WriteLine("─────────────────────────────");
        Console.ResetColor();

        // Respuesta estándar
        var response = new WebApiResponseDto<object>
        {
            ResponseCode = responseCode,
            ResponseMessage = responseMessage,
            ResponseData = null
        };

        context.Response.StatusCode = httpStatus;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    });
});

// Configure the HTTP request pipeline.
app.UseSwagger(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
});

app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(corsPolicy);

// Registro del middleware de validación de modelos
app.UseMiddleware<ValidationMiddleware>();
// app.UseMiddleware<ExpiredTokenMiddleware>();

// Autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
