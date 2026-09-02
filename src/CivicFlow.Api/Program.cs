using CivicFlow.Infrastructure;
using System.Text;
using CivicFlow.Api.Auth;
using CivicFlow.Api.Common;
using CivicFlow.Api.Background;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Some container platforms inject PORT at runtime. Bind explicitly to every
// interface while retaining ASPNETCORE_HTTP_PORTS/launchSettings behaviour.
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort))
{
    if (!int.TryParse(platformPort, out var parsedPort) || parsedPort is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be a valid TCP port number.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

// The Windows Event Log provider requires elevated source access and can mask the
// original request exception in local/non-service hosting. Structured console/debug
// providers work consistently in containers, developer terminals and CI.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<TokenService>();
builder.Services.AddHostedService<SlaMonitorWorker>();
builder.Services.AddScoped<AttachmentStorageMaintenance>();
builder.Services.AddHostedService<AttachmentCleanupWorker>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are missing.");
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var configuredClientOrigin = builder.Configuration["ClientOrigin"];
if (string.IsNullOrWhiteSpace(configuredClientOrigin))
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("ClientOrigin must be configured outside local development and testing.");
    configuredClientOrigin = "http://localhost:5173";
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
    {
        policy
            .WithOrigins(configuredClientOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

await DatabaseStartup.InitialiseAsync(app.Services);

app.Run();

public partial class Program;
