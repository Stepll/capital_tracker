using System.Text;
using System.Text.Json.Serialization;
using Anthropic;
using CapitalTracker.Application.Auth;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using CapitalTracker.Infrastructure.Ai;
using CapitalTracker.Infrastructure.Auth;
using CapitalTracker.Infrastructure.MarketData;
using CapitalTracker.Infrastructure.Persistence;
using CapitalTracker.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CapitalTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<CapitalTrackerDbContext>());

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<UserSeeder>();
builder.Services.AddScoped<SectorSeeder>();

builder.Services.Configure<AesEncryptionOptions>(builder.Configuration.GetSection(AesEncryptionOptions.SectionName));
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

// Fail fast on a missing/malformed key at startup rather than on the first
// request that happens to touch a holding's secret attributes.
var encryptionKey = builder.Configuration.GetSection(AesEncryptionOptions.SectionName).Get<AesEncryptionOptions>()
    ?? throw new InvalidOperationException("Encryption configuration section is missing.");
if (Convert.FromBase64String(encryptionKey.Key).Length != 32)
    throw new InvalidOperationException("Encryption:Key must decode to exactly 32 bytes (AES-256).");

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.Configure<InsightsOptions>(builder.Configuration.GetSection(InsightsOptions.SectionName));

// Same fail-fast treatment as the encryption key: a missing LLM key should stop the
// deploy, not surface as a 500 the first time someone opens a holding and clicks
// "analyse" — by which point the container is live and the failure looks like a bug.
const string AnthropicKeyHelp =
    "Set Anthropic:ApiKey — in Docker via ANTHROPIC_API_KEY in .env, locally via "
    + "`dotnet user-secrets set \"Anthropic:ApiKey\" \"sk-ant-...\"` in src/CapitalTracker.Api.";

builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));
var anthropicOptions = builder.Configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>()
    ?? throw new InvalidOperationException($"Anthropic configuration section is missing. {AnthropicKeyHelp}");
if (string.IsNullOrWhiteSpace(anthropicOptions.ApiKey))
    throw new InvalidOperationException($"Anthropic:ApiKey is empty. {AnthropicKeyHelp}");

builder.Services.AddSingleton(new AnthropicClient
{
    ApiKey = anthropicOptions.ApiKey,
    // Well under the SDK's 10-minute default. An analysis runs 1–3 minutes; past five
    // something is wedged, and holding the socket open only delays the error.
    Timeout = TimeSpan.FromMinutes(5),
});

// Finnhub is the deliberate opposite: no key simply means analyses run on web search
// alone, which is already the only path for holdings without a ticker. Nothing to
// validate at startup — see FinnhubOptions.
builder.Services.Configure<FinnhubOptions>(builder.Configuration.GetSection(FinnhubOptions.SectionName));
builder.Services.AddHttpClient<FinnhubClient>(client =>
{
    client.BaseAddress = new Uri("https://finnhub.io/");
    // Short on purpose: this runs inside a request the user is watching, and market
    // data is optional garnish. Better to skip it than to stall the whole analysis.
    client.Timeout = TimeSpan.FromSeconds(8);
});

builder.Services.AddScoped<IHoldingAnalysisGenerator, AnthropicHoldingAnalysisGenerator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the handler silently remaps "sub" to
        // ClaimTypes.NameIdentifier, so User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        // returns null even though the token has a "sub" claim.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires a valid JWT unless explicitly marked [AllowAnonymous]
    // — this is a single-user app exposed to the internet, so "closed by default" matters.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Frontend is deployed separately (Vercel), so it's a different origin — allow it,
// plus local dev, plus any Vercel preview deployment (random *.vercel.app subdomains).
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
                configuredOrigins.Contains(origin)
                || origin is "http://localhost:5173" or "http://localhost:3000"
                || Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CapitalTrackerDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<UserSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<SectorSeeder>().SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
