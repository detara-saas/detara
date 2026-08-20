using System.Text;
using System.Threading.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Detara.Api.Autenticacao;
using Detara.Api.Erros;
using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Infrastructure;
using Detara.Infrastructure.Persistencia;
using Detara.Contracts.Autorizacao;
using Detara.Application.Plataforma;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;

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

var platformJwtOptions = ObterPlatformJwtOptions(builder);
ValidarPlatformJwt(platformJwtOptions, jwtOptions);
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (string.IsNullOrWhiteSpace(keyRingPath))
{
    keyRingPath = Path.Combine(builder.Environment.ContentRootPath, "data", "data-protection-keys");
}
else if (!Path.IsPathFullyQualified(keyRingPath))
{
    keyRingPath = Path.GetFullPath(keyRingPath, builder.Environment.ContentRootPath);
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Secao));
builder.Services.AddSingleton<IOptions<PlatformJwtOptions>>(Options.Create(platformJwtOptions));
builder.Services.AddDataProtection()
    .SetApplicationName("Detara.Platform")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
builder.Services.AdicionarApplication();
builder.Services.AdicionarInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioContexto, HttpUsuarioContexto>();
builder.Services.AddScoped<IContextoAdministradorPlataforma, HttpAdministradorPlataformaContexto>();
builder.Services.AddScoped<ITokenServico, JwtTokenServico>();
builder.Services.AddScoped<ITokenPlataformaServico, PlatformJwtTokenServico>();
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
    options.AddPolicy("platform-login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("platform-mfa", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));
    options.AddPolicy("convite-administrador", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
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
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = EsquemasAutenticacao.Tenant;
        options.DefaultChallengeScheme = EsquemasAutenticacao.Tenant;
    })
    .AddJwtBearer(EsquemasAutenticacao.Tenant, options =>
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
    })
    .AddJwtBearer(EsquemasAutenticacao.Plataforma, options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = platformJwtOptions.Emissor,
            ValidateAudience = true,
            ValidAudience = platformJwtOptions.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(platformJwtOptions.ChaveAssinatura)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null ||
                    principal.FindFirst("identidade")?.Value != "platform_admin" ||
                    principal.FindFirst("amr")?.Value != "mfa" ||
                    !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var administradorId) ||
                    !long.TryParse(principal.FindFirst("versao_seguranca")?.Value, out var versao))
                {
                    context.Fail("Token inválido.");
                    return;
                }

                var validador = context.HttpContext.RequestServices
                    .GetRequiredService<IAutenticacaoPlataformaServico>();
                if (!await validador.RevalidarAsync(
                        administradorId,
                        versao,
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
        options.AddPolicy(permissao, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("permissao", permissao));
    }
    options.AddPolicy(EsquemasAutenticacao.PolicyAdministradorPlataforma, policy => policy
        .AddAuthenticationSchemes(EsquemasAutenticacao.Plataforma)
        .RequireAuthenticatedUser()
        .RequireClaim("identidade", "platform_admin")
        .RequireClaim("amr", "mfa"));
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
            out var atualizadoEmTicks) ||
        !long.TryParse(
            principal.FindFirst("empresa_versao_seguranca")?.Value,
            out var empresaVersaoSeguranca))
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
        empresaVersaoSeguranca,
        permissoes);
}

static PlatformJwtOptions ObterPlatformJwtOptions(WebApplicationBuilder builder)
{
    var configurado = builder.Configuration.GetSection(PlatformJwtOptions.Secao).Get<PlatformJwtOptions>()
        ?? new PlatformJwtOptions();
    var chave = configurado.ChaveAssinatura;
    if (string.IsNullOrWhiteSpace(chave) && !builder.Environment.IsProduction())
    {
        chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    return new PlatformJwtOptions
    {
        Emissor = configurado.Emissor,
        Audiencia = configurado.Audiencia,
        ChaveAssinatura = chave,
        ExpiracaoMinutos = configurado.ExpiracaoMinutos
    };
}

static void ValidarPlatformJwt(
    PlatformJwtOptions platform,
    JwtOptions tenant)
{
    if (string.IsNullOrWhiteSpace(platform.Emissor) ||
        string.IsNullOrWhiteSpace(platform.Audiencia) ||
        Encoding.UTF8.GetByteCount(platform.ChaveAssinatura) < 32 ||
        platform.ExpiracaoMinutos is < 30 or > 60)
    {
        throw new InvalidOperationException(
            "PlatformJwt exige emissor, audiência, chave com pelo menos 32 bytes e expiração entre 30 e 60 minutos.");
    }

    if (string.Equals(
            platform.ChaveAssinatura,
            tenant.ChaveAssinatura,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException("As chaves JWT de Platform e tenant devem ser distintas.");
    }
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

    var publicBaseUrl = builder.Configuration["Web:PublicBaseUrl"];
    if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri) ||
        publicUri.Scheme != Uri.UriSchemeHttps ||
        publicUri.IsLoopback ||
        publicUri.AbsolutePath != "/")
    {
        throw new InvalidOperationException(
            "Web__PublicBaseUrl deve ser uma origem HTTPS pública em produção.");
    }

    var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    if (string.IsNullOrWhiteSpace(keyRingPath) || !Path.IsPathFullyQualified(keyRingPath))
    {
        throw new InvalidOperationException(
            "DataProtection__KeyRingPath deve apontar para armazenamento persistente absoluto em produção.");
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
