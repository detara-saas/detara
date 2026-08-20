using System.Text;
using System.Threading.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using Detara.Api.Autenticacao;
using Detara.Api.Erros;
using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Infrastructure;
using Detara.Infrastructure.Persistencia;
using Detara.Contracts.Autorizacao;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});

var origensCors = builder.Configuration
    .GetSection("Cors:OrigensPermitidas")
    .Get<string[]>() ?? [];
ValidarConfiguracaoDeProducao(builder, origensCors);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>()
    ?? throw new InvalidOperationException("A configuração JWT deve ser informada.");

if (Encoding.UTF8.GetByteCount(jwtOptions.ChaveAssinatura) < 32)
{
    throw new InvalidOperationException(
        "Jwt__ChaveAssinatura deve possuir pelo menos 32 bytes e vir de secret ou variável de ambiente.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Emissor) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audiencia) ||
    jwtOptions.ExpiracaoMinutos is <= 0 or > 1440)
{
    throw new InvalidOperationException(
        "Emissor, audiência e expiração JWT (entre 1 e 1440 minutos) devem ser configurados.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Secao));
builder.Services.AdicionarApplication();
builder.Services.AdicionarInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioContexto, HttpUsuarioContexto>();
builder.Services.AddScoped<ITokenServico, JwtTokenServico>();
builder.Services.AddExceptionHandler<TratadorGlobalExcecoes>();
builder.Services.AddProblemDetails();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DetaraDbContext>("database");
builder.Services.AddCors(options => options.AddPolicy("Web", policy => policy
    .WithOrigins(origensCors)
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("notificacao-teste", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        $"{httpContext.User.FindFirst("empresa_id")?.Value}:{httpContext.User.FindFirst("sub")?.Value}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));
    options.AddPolicy("health", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            Detara.Contracts.Comum.RespostaApi<object>.Falha(
                "Muitas tentativas. Aguarde e tente novamente.",
                "limite_tentativas"),
            cancellationToken);
    };
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Emissor,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.ChaveAssinatura)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var identidade = ExtrairIdentidade(context.Principal);
                if (identidade is null)
                {
                    context.Fail("Token inválido.");
                    return;
                }

                var validador = context.HttpContext.RequestServices
                    .GetRequiredService<IValidadorIdentidadeAutenticada>();
                if (!await validador.EhValidaAsync(
                        identidade,
                        context.HttpContext.RequestAborted))
                {
                    context.Fail("Token revogado.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    foreach (var permissao in Permissoes.Todas)
    {
        options.AddPolicy(permissao, policy => policy.RequireClaim("permissao", permissao));
    }
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Detara API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.ValidarMigrationsDesenvolvimentoAsync();
    await app.Services.InicializarDesenvolvimentoAsync(app.Configuration);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/health"))
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
    }

    await next();
});
app.UseCors("Web");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health")
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]))
    .AllowAnonymous()
    .RequireRateLimiting("health");

app.Run();

static IdentidadeToken? ExtrairIdentidade(System.Security.Claims.ClaimsPrincipal? principal)
{
    if (principal is null ||
        !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var usuarioId) ||
        !Guid.TryParse(principal.FindFirst("empresa_id")?.Value, out var empresaId) ||
        !Guid.TryParse(principal.FindFirst("perfil_id")?.Value, out var perfilId) ||
        !long.TryParse(
            principal.FindFirst("usuario_atualizado_ticks")?.Value,
            out var atualizadoEmTicks))
    {
        return null;
    }

    var permissoes = principal.FindAll("permissao")
        .Select(claim => claim.Value)
        .ToArray();
    return new IdentidadeToken(
        usuarioId,
        empresaId,
        perfilId,
        atualizadoEmTicks,
        permissoes);
}

static void ValidarConfiguracaoDeProducao(
    WebApplicationBuilder builder,
    IReadOnlyCollection<string> origensCors)
{
    if (origensCors.Any(origem => !EhOrigemCorsValida(origem)))
    {
        throw new InvalidOperationException(
            "Cors__OrigensPermitidas deve conter apenas origens absolutas explícitas.");
    }

    if (!builder.Environment.IsProduction())
    {
        return;
    }

    var hosts = builder.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(hosts) ||
        hosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => host.Contains('*')))
    {
        throw new InvalidOperationException(
            "AllowedHosts deve listar os hosts públicos explícitos em produção.");
    }

    if (origensCors.Any(origem =>
            !Uri.TryCreate(origem, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.IsLoopback))
    {
        throw new InvalidOperationException(
            "CORS em produção aceita somente origens HTTPS não locais.");
    }
}

static bool EhOrigemCorsValida(string origem)
{
    if (string.IsNullOrWhiteSpace(origem) ||
        origem.Contains('*') ||
        !Uri.TryCreate(origem, UriKind.Absolute, out var uri) ||
        (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
        return false;
    }

    return uri.AbsolutePath == "/" && !origem.EndsWith('/');
}

public partial class Program;
