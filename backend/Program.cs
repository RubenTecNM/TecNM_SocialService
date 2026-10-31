using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Auth;
using TecNM.Api.Modules.Catalogs;
using TecNM.Api.Modules.Companies;
using TecNM.Api.Modules.Students;

const string FrontendCors = "Frontend";

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings__DefaultConnection en la configuración.");
}

var secret = configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
{
    throw new InvalidOperationException(
        "Jwt__SecretKey es obligatorio y debe contener al menos 32 bytes UTF-8.");
}

var issuer = configuration["Jwt:Issuer"];
var audience = configuration["Jwt:Audience"];
var lifetimeHours = configuration.GetValue<int?>("Jwt:LifetimeHours");
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || lifetimeHours != 8)
{
    throw new InvalidOperationException(
        "Jwt:Issuer, Jwt:Audience y Jwt:LifetimeHours=8 son obligatorios.");
}

var rawAllowedOrigins = configuration["Cors:AllowedOrigin"];
var allowedOrigins = (rawAllowedOrigins ?? "")
    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .ToArray();

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors__AllowedOrigin es obligatorio.");
}

foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
        || (originUri.Scheme != Uri.UriSchemeHttps && !originUri.IsLoopback))
    {
        throw new InvalidOperationException(
            $"El origen CORS '{origin}' debe ser un origen HTTPS absoluto o loopback local.");
    }
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
var jwtSettings = new JwtSettings(issuer, audience, lifetimeHours.Value, signingKey);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();

builder.Services.AddDbContext<TecNMDbContext>((serviceProvider, options) =>
{
    var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
    options.UseNpgsql(connectionString)
           .AddInterceptors(auditInterceptor);
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CareersService>();
builder.Services.AddScoped<AcademicPeriodsService>();
builder.Services.AddScoped<SocialServiceProgramsService>();
builder.Services.AddScoped<InstitutionalAuthoritiesService>();
builder.Services.AddScoped<DocumentSeriesService>();
builder.Services.AddScoped<CompaniesService>();
builder.Services.AddScoped<SupervisorsService>();
builder.Services.AddScoped<AgreementsService>();
builder.Services.AddScoped<StudentsService>();
builder.Services.AddScoped<StudentImportsService>();

builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["auth_token"];
                if (string.IsNullOrEmpty(token))
                {
                    context.NoResult();
                }
                else
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

static void LoadDotEnv()
{
    var searchDir = new DirectoryInfo(Directory.GetCurrentDirectory());
    DirectoryInfo? backendDir = null;

    if (File.Exists(Path.Combine(searchDir.FullName, "TecNM.Api.csproj")))
    {
        backendDir = searchDir;
    }
    else if (Directory.Exists(Path.Combine(searchDir.FullName, "backend")))
    {
        backendDir = new DirectoryInfo(Path.Combine(searchDir.FullName, "backend"));
    }
    else
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDir != null)
        {
            if (File.Exists(Path.Combine(baseDir.FullName, "TecNM.Api.csproj")))
            {
                backendDir = baseDir;
                break;
            }

            baseDir = baseDir.Parent;
        }
    }

    if (backendDir == null)
    {
        return;
    }

    var envPath = Path.Combine(backendDir.FullName, ".env");
    if (!File.Exists(envPath))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
