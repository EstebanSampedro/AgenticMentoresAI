#region using
using Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Conversations;
using Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Subscriptions;
using Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Summaries;
using Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Users;
using Academikus.AgenteInteligenteMentoresTareas.Business.Common;
using Academikus.AgenteInteligenteMentoresTareas.Business.Common.KeyVault;
using Academikus.AgenteInteligenteMentoresTareas.Business.Hubs;
using Academikus.AgenteInteligenteMentoresTareas.Business.Repositories;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.AI;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Attachments;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Backend;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.BannerWebApi;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.BatchService;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.ConversationLifecycle;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Conversations;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.DailySummaries;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.GraphNotification;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Services;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Subscriptions;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.Command.Core;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Common;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Academikus.AgenteInteligenteMentoresTareas.Utility.WebApi;
using Academikus.AgenteInteligenteMentoresTareas.WebApi.Middleware;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebApiTemplate.AuthorizeDLL;
using WebApiTemplate.AuthorizeDLL.DatabaseConnection;
using WebApiTemplate.AuthorizeDLL.Model;
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
builder.Services.Configure<ConversationTimeoutOptions>(
    configuration.GetSection("Conversation:Timeout"));
builder.Services.Configure<StudentInformationRefreshOptions>(
    builder.Configuration.GetSection("StudentInfoRefresh"));
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));
builder.Services.Configure<BannerWebApiOptions>(
    builder.Configuration.GetSection("BannerWebApi"));
builder.Services.Configure<ServiceAccountOptions>(
    configuration.GetSection("ServiceAccount"));

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

// Add services to the container.
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

//Log4net Declaration
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Academikus.AgenteInteligenteMentoresTareas.WebApi", Version = "v1" });
    //c.DescribeAllParametersInCamelCase();
    var filePath = Path.Combine(System.AppContext.BaseDirectory, "Academikus.AgenteInteligenteMentoresTareas.WebApi.xml");
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
    // Agrega en swagger la seguridad de tipo Basic para poder insertar credenciales b�sicas y consumir endpoints protegidos con UdlaBasicDbAuthorize
    // TODO: Agregar a plantilla
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
builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

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

//builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddControllersWithViews();

//Authentication Scheme
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["AzureAd:Authority"];
        options.Audience = configuration["AzureAd:ApiUri"];

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        // Permitir tokens en conexiones WebSocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    // Console.WriteLine($"Token detectado en conexión a {path}");
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var user = context.Principal;
                var name = user?.Identity?.Name ?? "(sin nombre)";
                Console.WriteLine($"Token válido para usuario: {name}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Falló autenticación JWT: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

EnvironmentHelper.Initialize(builder.Configuration, builder.Environment);

//Dependency Injection
builder.Services.AddScoped<IWebApiInvoker, WebApiInvoker>();
builder.Services.AddScoped<IWebApiInvokerConecte, WebApiInvokerConecte>();
builder.Services.AddScoped<ICommandExecutor, CommandExecutor>();
builder.Services.AddScoped<WebApiBO, WebApiBO>();
builder.Services.AddScoped<ILoggerDbRepository, LoggerDbRepository>();

//Agregar secciones de configuraci�n
builder.Services.AddSingleton<GraphServiceClient>(provider =>
{
    var tenantId = configuration["AzureAd:TenantId"];
    var clientId = configuration["AzureAd:ClientId"];
    var clientSecret = configuration["AzureAd:ClientSecret"];

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

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

builder.Services.AddSingleton(provider =>
{
    var eventHubConnectionString = configuration["EventHub:ConnectionString"];
    var eventHubName = configuration["EventHub:Name"];
    var blobConnectionString = configuration["StorageAccount:ConnectionString"];
    var blobContainerName = configuration["StorageAccount:CheckpointContainer"];
    var consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

    var storageClient = new BlobContainerClient(blobConnectionString, blobContainerName);

    return new EventProcessorClient(storageClient, consumerGroup, eventHubConnectionString, eventHubName);
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = false;
});

builder.Services.AddDbContext<DBContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IaiClientService, AiClientService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IBackendApiClientService, BackendApiClientService>();
builder.Services.AddHttpClient<IBannerWebApiService, BannerWebApiService>();
builder.Services.AddScoped<IAiBatchService, AiBatchService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddHostedService<ConversationFinalizationService>();
builder.Services.AddScoped<IConversationLifecycleService, ConversationLifecycleService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddHostedService<DailySummariesService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddHostedService<StudentInfoRefreshService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IGraphService, GraphService>();
builder.Services.AddHostedService<GraphNotificationConsumer>();
builder.Services.AddScoped<IGraphNotificationService, GraphNotificationService>();
builder.Services.AddHostedService<SubscriptionRenewalService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<MicrosoftGraphAuthService>();

builder.Services.AddHostedService<ConversationProcessor>();

builder.Services.Configure<GraphOptions>(
    builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<AiBatchingOptions>(
    configuration.GetSection("AiBatching"));

builder.Services.AddOptions();

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
});

app.UseSwaggerUI();

app.UseRouting();

// Usar CORS antes de los endpoints
app.UseCors(corsPolicy);

//****Se agrega con la configuracion de la dll autorizacion 
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ChatHub>("/chathub").RequireCors(corsPolicy);
app.UseEndpoints(options =>
{
    options.MapControllers();
    // options.MapDefaultControllerRoute();
});

app.UseHttpsRedirection();

// Registro del middleware de validaci�n de modelos
app.UseMiddleware<ValidationMiddleware>();
app.UseMiddleware<ExpiredTokenMiddleware>();

app.MapControllers();

app.Run();
