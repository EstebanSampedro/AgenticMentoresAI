using Academikus.AnalysisMentoresVerdes.Business;
using Academikus.AnalysisMentoresVerdes.Business.Abstractions;
using Academikus.AnalysisMentoresVerdes.Business.Extensions;
using Academikus.AnalysisMentoresVerdes.Business.Services;
using Academikus.AnalysisMentoresVerdes.Data.Ef;
using Academikus.AnalysisMentoresVerdes.Data.Extensions;
using Academikus.AnalysisMentoresVerdes.Entity.Common;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;
using Academikus.AnalysisMentoresVerdes.Proxy.Extensions;
using Academikus.AnalysisMentoresVerdes.WebApi.Jobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.ClientModel;
using System.Net.Http.Headers;
using System.Text;
using WebApiTemplate.AuthorizeDLL;
using WebApiTemplate.AuthorizeDLL.DatabaseConnection;
using WebApiTemplate.AuthorizeDLL.Model;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging (log4net) ----------
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

// ---------- Controllers / JSON ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = null; });

builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Academikus.AnalysisMentoresVerdes.WebApi", Version = "v1" });

    var xml = Path.Combine(AppContext.BaseDirectory, "Academikus.AnalysisMentoresVerdes.WebApi.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);

    // Bearer
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Insertar token Bearer",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="Bearer" }}, Array.Empty<string>() }
    });

    // Basic (si usas UdlaBasicDbAuthorize)
    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Description = "Basic auth",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Scheme = "basic",
        Type = SecuritySchemeType.Http
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Basic" }}, new List<string>() }
    });
});

// ---------- Health ----------
builder.Services.AddHealthChecks();

// ---------- DB Context (autorización) ----------
builder.Services.AddDbContext<WebApiDBEntities>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("WebApiDBEntities"),
        sql => sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null));
});

builder.Services.AddDbContext<AnalysisDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
// ---------- Auth (JWT) ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        var issuer = builder.Configuration["AutSch:Issuer"];
        var audiences = builder.Configuration.GetSection("AutSch:ValidAudiences").Get<List<string>>() ?? new();
        var key = builder.Configuration["AutSch:Key"] ?? throw new InvalidOperationException("AutSch:Key not configured");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidIssuer = issuer,
            ValidAudiences = audiences,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

// ---------- Options ----------
builder.Services.AddOptions();
builder.Services.Configure<AnalysisOptions>(builder.Configuration.GetSection("Analysis"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
//builder.Services.Configure<AppSetting>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<ConnectionString>(builder.Configuration.GetSection("connectionStrings"));

// ---------- Azure OpenAI SDK v2 ----------
var aoaiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var aoaiKey = builder.Configuration["AzureOpenAI:Key"];

if (string.IsNullOrWhiteSpace(aoaiEndpoint) || string.IsNullOrWhiteSpace(aoaiKey))
    throw new InvalidOperationException("AzureOpenAI Endpoint/Key no configurados.");

builder.Services.AddSingleton(
    new Azure.AI.OpenAI.AzureOpenAIClient(
        new Uri(aoaiEndpoint),
        new ApiKeyCredential(aoaiKey)
    )
);
// ---------- DI ----------
builder.Services.AddScoped<WebApiBO, WebApiBO>();
builder.Services.AddScoped<IGenerativeClient, AzureOpenAiClient>();
builder.Services.AddScoped<IWeeklyAnalysisService, WeeklyAnalysisService>();

builder.Services.AddBusiness(builder.Configuration);
builder.Services.AddData(builder.Configuration);
builder.Services.AddProxy(builder.Configuration);
// Background 
builder.Services.AddHostedService<WeeklyAnalysisBackgroundService>();

// ? Registro del cliente REST de Azure OpenAI
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateOnBuild = true;
    o.ValidateScopes = true;
});

var app = builder.Build();

// ---------- Swagger ----------
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger(c => c.SerializeAsV2 = true);
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Health
app.MapHealthChecks("/health");

// Controllers
app.MapControllers();

app.Run();