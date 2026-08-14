using System.Text;
using System.Threading.RateLimiting;
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
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DetaraDbContext>("database");
builder.Services.AddCors(options => options.AddPolicy("Web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [])
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
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(
            Detara.Contracts.Comum.RespostaApi<object>.Falha(
                "Muitas tentativas. Aguarde um minuto e tente novamente.",
                "limite_tentativas"),
            cancellationToken);
    };
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
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
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    foreach (var permissao in Permissoes.ModulosClientesVeiculos)
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
    await app.Services.InicializarDesenvolvimentoAsync(builder.Configuration);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
