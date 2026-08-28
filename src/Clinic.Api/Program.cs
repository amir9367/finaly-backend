using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Services;
using Clinic.Api.Services.Excel;
using Clinic.Api.Services.Sms;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Fail-fast configuration validation ----------
// Secrets must come from the environment (compose / .env). A missing or weak
// secret must stop startup — never fall back to a committed value.

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Secret is missing or shorter than 32 characters. " +
        "Provide it via the environment (Jwt__Secret, e.g. JWT_SECRET in .env).");

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"])
    || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

// Outside Development the console sender would silently swallow every OTP and
// log patient phone numbers. Refuse to run that way.
var smsProvider = builder.Configuration["Sms:Provider"] ?? "Console";
if (!builder.Environment.IsDevelopment()
    && !smsProvider.Equals("Melipayamak", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        "Refusing to start outside Development with Sms:Provider != 'Melipayamak' " +
        "(patients would never receive SMS and OTP codes would be written to logs).");

// ---------- Services ----------

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Clinic Appointment API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/admin/auth/login",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// Pooled contexts: supports both PostgreSQL (production) and SQLite (local demo without Docker).
// If connection string looks like SQLite (Data Source=...), use Sqlite; otherwise use Npgsql.
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "";
var isSqlite = connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase) || connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
if (isSqlite)
{
    builder.Services.AddDbContextPool<AppDbContext>(options => options
        .UseSqlite(connectionString)
        .UseSnakeCaseNamingConvention());
}
else
{
    builder.Services.AddDbContextPool<AppDbContext>(options => options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());
}

// SMS: provider chosen by config; Console sender exists for Development only
// (startup above refuses it everywhere else).
builder.Services.AddHttpClient<MelipayamakSmsSender>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<ConsoleSmsSender>();
builder.Services.AddScoped<ISmsSender>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = configuration["Sms:Provider"] ?? "Console";
    return provider.Equals("Melipayamak", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<MelipayamakSmsSender>()
        : sp.GetRequiredService<ConsoleSmsSender>();
});
builder.Services.AddScoped<ISmsService, SmsService>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IExcelSyncService, ExcelSyncService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// CORS is only needed when a browser calls the API cross-origin. Both panels
// reach the API same-origin through their nginx proxy; Vite's dev server
// proxies /api as well, so an open policy in Development costs nothing.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Rate limiting — there was none anywhere. Keys are client IPs (real ones,
// thanks to the forwarded-headers middleware below).
static string ClientIpKey(HttpContext ctx) =>
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global backstop for every request (including endpoints without a named
    // policy — admin routes, doctors list, availability). Generous enough to
    // never disturb real users, tight enough that a flooding client cannot
    // hammer the database unchecked.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Admin login: strict per-IP window against online password guessing.
    options.AddPolicy("auth-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Public booking: caps schedule-exhaustion and SMS-bombing throughput.
    options.AddPolicy("public-book", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Lookup + cancel flow: sized around the human flow (one code ≈ minutes).
    options.AddPolicy("otp-request", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.AddPolicy("otp-confirm", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.AddPolicy("public-lookup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Public reads (doctors list, availability): the availability query joins
    // schedules + bookings for 14 days — uncached hammering would show up as DB
    // load. 60/min per IP is far above any human browsing pattern.
    options.AddPolicy("public-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// ---------- Pipeline ----------

// Trust X-Forwarded-* from the reverse proxies (nginx containers). Networks
// are cleared so compose-network proxies are accepted; the compose network is
// the trust boundary.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseMiddleware<ExceptionsMiddleware>();

if (!app.Environment.IsDevelopment()) app.UseHsts();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

// ---------- Database bootstrap (schema + constraint + seed admin) ----------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql())
    {
        await DbBootstrapper.InitializeAsync(db, app.Configuration, app.Logger);
    }
    else if (db.Database.IsSqlite())
    {
        await DbBootstrapper.InitializeSqliteAsync(db, app.Configuration, app.Logger);
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

app.Run();
