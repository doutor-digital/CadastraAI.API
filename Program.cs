using System.Text;
using System.Text.Json.Serialization;
using CadastraAI.API.Auth;
using CadastraAI.API.Cache;
using CadastraAI.API.Data;
using CadastraAI.API.Email;
using CadastraAI.API.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ----- Database -----
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(connectionString));

// ----- Redis cache (dashboard) -----
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = redisConnection;
        opts.InstanceName = "cadastraai:";
    });
    builder.Services.AddSingleton<IDashboardCache, RedisDashboardCache>();
}
else
{
    // Dev fallback: in-memory IDistributedCache so the API still boots without Redis configured.
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<IDashboardCache, RedisDashboardCache>();
}

// ----- Auth options -----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Authentication:Jwt"));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection("Authentication:Google"));

// ----- Auth services -----
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

// ----- Storage -----
builder.Services.AddSingleton<ILocalFileStorage, LocalFileStorage>();

// ----- Email -----
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "None";
if (string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    var resendBase = builder.Configuration.GetValue<string>("Email:Resend:BaseUrl") ?? "https://api.resend.com";
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
    {
        client.BaseAddress = new Uri(resendBase);
        client.Timeout = TimeSpan.FromSeconds(15);
    });
}
else
{
    builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
}

// ----- Authentication / Authorization -----
var jwtSection = builder.Configuration.GetSection("Authentication:Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Authentication:Jwt:Key is not configured.");
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Authentication:Jwt:Key must be at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });

builder.Services.AddAuthorization();

// ----- CORS for the Next.js frontend -----
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ----- Web stack -----
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Accept enum values as strings ("Member", "Admin") in addition to numbers (0/1/2).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS redirection only in non-Development to keep the dev URL `http://localhost:5299` usable.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ----- Static uploads -----
var webRoot = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRoot))
{
    webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
}
var uploadsRoot = Path.Combine(webRoot, "uploads");
Directory.CreateDirectory(Path.Combine(uploadsRoot, "empresa-logos"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=31536000,immutable";
    },
});

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
