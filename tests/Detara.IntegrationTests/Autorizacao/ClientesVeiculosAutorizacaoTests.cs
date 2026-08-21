using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Detara.Application.Abstracoes;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Contracts.Onboarding;
using Detara.Domain.Entidades;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Autorizacao;

public sealed class ClientesVeiculosAutorizacaoTests : IAsyncLifetime
{
    private readonly DetaraApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.InicializarBancoAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task UsuarioSemClientesVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComClientesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var response = await _client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnboardingTenantAutenticado_NaoExigePermissaoNova()
    {
        var response = await _client.GetAsync("/api/onboarding");
        var corpo = await response.Content.ReadFromJsonAsync<RespostaApi<OnboardingEmpresaResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(corpo?.Sucesso);
        Assert.Equal(5, corpo?.Resultado?.QuantidadeTotal);
        Assert.True(corpo?.Resultado?.Etapas.Single(x => x.Codigo == "empresa").Concluida);
    }

    [Fact]
    public async Task OnboardingSemPermissao_NaoOfereceCtaProibidoECatalogoPermanece403()
    {
        var onboarding = await _client.GetFromJsonAsync<RespostaApi<OnboardingEmpresaResponse>>(
            "/api/onboarding");
        var tentativaCatalogo = await _client.PostAsJsonAsync("/api/servicos", new { });

        Assert.False(onboarding?.Resultado?.Etapas.Single(x => x.Codigo == "catalogo").PodeExecutar);
        Assert.Equal(HttpStatusCode.Forbidden, tentativaCatalogo.StatusCode);
    }

    [Fact]
    public async Task ClienteDeOutroTenant_NaoPodeSerLidoNemEditadoPorId()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar, Permissoes.ClientesEditar);

        var leitura = await _client.GetAsync($"/api/clientes/{_factory.ClienteOutroTenantId}");
        var edicao = await _client.PutAsJsonAsync(
            $"/api/clientes/{_factory.ClienteOutroTenantId}",
            new SalvarClienteRequest(
                "Tentativa indevida",
                "PessoaFisica",
                "39053344705",
                null,
                null,
                null,
                null,
                null));

        Assert.Equal(HttpStatusCode.NotFound, leitura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edicao.StatusCode);
        Assert.Equal("Cliente outro tenant", await _factory.ObterNomeClienteGlobalAsync(
            _factory.ClienteOutroTenantId));
    }

    [Fact]
    public async Task CamposProtegidosExtras_NaoControlamTenantNemEstadoDoCliente()
    {
        UsarPermissoes(Permissoes.ClientesCriar);
        var payload = new
        {
            Nome = "Cliente mass assignment",
            TipoPessoa = "PessoaFisica",
            CpfCnpj = "39053344705",
            Telefone = (string?)null,
            WhatsApp = (string?)null,
            Email = (string?)null,
            DataNascimento = (DateOnly?)null,
            Observacao = (string?)null,
            EmpresaId = _factory.EmpresaOutroTenantId,
            EhAtivo = false,
            CriadoEmUtc = DateTime.UnixEpoch
        };

        var response = await _client.PostAsJsonAsync("/api/clientes", payload);
        var corpo = await response.Content.ReadFromJsonAsync<RespostaApi<ClienteDetalheResponse>>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(corpo?.Resultado);
        var estado = await _factory.ObterEstadoClienteGlobalAsync(corpo.Resultado.Id);
        Assert.Equal(_factory.EmpresaId, estado.EmpresaId);
        Assert.True(estado.EhAtivo);
        Assert.NotEqual(DateTime.UnixEpoch, estado.CriadoEmUtc);
    }

    [Fact]
    public async Task Login_FalhasNaoPermitemEnumerarEmail()
    {
        var emailInexistente = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest("inexistente@detara.local", "senha-incorreta"));
        var senhaIncorreta = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest(DetaraApiFactory.EmailLogin, "senha-incorreta"));
        var respostaInexistente = await emailInexistente.Content
            .ReadFromJsonAsync<RespostaApi<object>>();
        var respostaIncorreta = await senhaIncorreta.Content
            .ReadFromJsonAsync<RespostaApi<object>>();

        Assert.Equal(HttpStatusCode.Unauthorized, emailInexistente.StatusCode);
        Assert.Equal(emailInexistente.StatusCode, senhaIncorreta.StatusCode);
        Assert.Equal(respostaInexistente?.Info, respostaIncorreta?.Info);
        Assert.Equal("credenciais_invalidas", respostaInexistente?.Erro?.Codigo);
        Assert.Equal(respostaInexistente?.Erro?.Codigo, respostaIncorreta?.Erro?.Codigo);
    }

    [Fact]
    public async Task LoginMembershipUnica_RetornaJwtTenantSemEtapaDeSelecao()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest(DetaraApiFactory.EmailLogin, DetaraApiFactory.SenhaLogin));
        var body = await response.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessao = Assert.IsType<LoginAutenticadoResponse>(body?.Resultado);
        Assert.Equal(_factory.EmpresaId, sessao.EmpresaId);
        Assert.False(string.IsNullOrWhiteSpace(sessao.Token));
    }

    [Fact]
    public async Task LoginComMesmaSenhaEmDoisTenants_ExigeSelecaoEEmiteJwtDoEscolhido()
    {
        await _factory.AdicionarMembershipOutroTenantAsync(DetaraApiFactory.SenhaLogin);
        var login = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest(DetaraApiFactory.EmailLogin, DetaraApiFactory.SenhaLogin));
        var body = await login.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>();
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResponse>(body?.Resultado);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(2, selecao.Empresas.Count);
        Assert.DoesNotContain(DetaraApiFactory.SenhaLogin, selecao.Challenge, StringComparison.Ordinal);

        var escolha = await _client.PostAsJsonAsync(
            "/api/autenticacao/selecionar-empresa",
            new SelecionarEmpresaRequest(selecao.Challenge, _factory.EmpresaOutroTenantId));
        var sessao = await escolha.Content
            .ReadFromJsonAsync<RespostaApi<LoginAutenticadoResponse>>();

        Assert.Equal(HttpStatusCode.OK, escolha.StatusCode);
        Assert.Equal(_factory.EmpresaOutroTenantId, sessao?.Resultado?.EmpresaId);
        Assert.False(string.IsNullOrWhiteSpace(sessao?.Resultado?.Token));
    }

    [Fact]
    public async Task SelecaoEmpresaForaDoChallenge_Retorna401Generico()
    {
        await _factory.AdicionarMembershipOutroTenantAsync(DetaraApiFactory.SenhaLogin);
        var login = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest(DetaraApiFactory.EmailLogin, DetaraApiFactory.SenhaLogin));
        var body = await login.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>();
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResponse>(body?.Resultado);

        var escolha = await _client.PostAsJsonAsync(
            "/api/autenticacao/selecionar-empresa",
            new SelecionarEmpresaRequest(selecao.Challenge, Guid.NewGuid()));
        var erro = await escolha.Content.ReadFromJsonAsync<RespostaApi<object>>();

        Assert.Equal(HttpStatusCode.Unauthorized, escolha.StatusCode);
        Assert.Equal("selecao_empresa_invalida", erro?.Erro?.Codigo);
    }

    [Fact]
    public async Task EmpresaDesativadaDepoisDoChallenge_NaoRecebeJwt()
    {
        await _factory.AdicionarMembershipOutroTenantAsync(DetaraApiFactory.SenhaLogin);
        var login = await _client.PostAsJsonAsync(
            "/api/autenticacao/login",
            new LoginRequest(DetaraApiFactory.EmailLogin, DetaraApiFactory.SenhaLogin));
        var body = await login.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>();
        var selecao = Assert.IsType<SelecaoEmpresaNecessariaResponse>(body?.Resultado);
        await _factory.DesativarEmpresaOutroTenantAsync();

        var escolha = await _client.PostAsJsonAsync(
            "/api/autenticacao/selecionar-empresa",
            new SelecionarEmpresaRequest(selecao.Challenge, _factory.EmpresaOutroTenantId));

        Assert.Equal(HttpStatusCode.Unauthorized, escolha.StatusCode);
    }

    [Fact]
    public async Task Login_RepetidoExcessivamente_Retorna429NoLimite()
    {
        var payload = new
        {
            Email = "usuario-inexistente@detara.local",
            Senha = "senha-incorreta"
        };

        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var resposta = await _client.PostAsJsonAsync("/api/autenticacao/login", payload);
            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        var bloqueada = await _client.PostAsJsonAsync("/api/autenticacao/login", payload);

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.Equal("application/json", bloqueada.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PesquisaComPayloadSql_NaoAlteraAQueryNemRetornaDados()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);

        var response = await _client.GetAsync(
            "/api/clientes?pesquisa=%27%20OR%201%3D1--&tamanhoPagina=25");
        var corpo = await response.Content.ReadFromJsonAsync<
            RespostaApi<PaginaResponse<ClienteListaResponse>>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(corpo?.Resultado);
        Assert.Empty(corpo.Resultado.Itens);
    }

    [Fact]
    public async Task OrdenacaoComComandoSql_EhRejeitadaEServicoPermaneceIntegro()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);

        var ataque = await _client.GetAsync(
            "/api/clientes?ordenacao=nome%3BDROP%20TABLE%20Clientes--");
        var verificacao = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.BadRequest, ataque.StatusCode);
        Assert.Equal(HttpStatusCode.OK, verificacao.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemClientesEditar_AlteracaoBloqueada()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var request = new SalvarClienteRequest(
            "Cliente alterado",
            "PessoaFisica",
            "52998224725",
            null,
            null,
            null,
            null,
            null);
        var response = await _client.PutAsJsonAsync($"/api/clientes/{_factory.ClienteId}", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemVeiculosVisualizar_Recebe403()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var response = await _client.GetAsync("/api/veiculos");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemConfiguracoesVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/configuracoes/operacao");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComConfiguracoesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ConfiguracoesVisualizar);
        var response = await _client.GetAsync("/api/configuracoes/operacao");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/configuracoes/operacao")]
    [InlineData("/api/configuracoes/operacao/checklist")]
    public async Task UsuarioSemConfiguracoesEditar_AlteracaoBloqueada(string rota)
    {
        UsarPermissoes(Permissoes.ConfiguracoesVisualizar);
        var response = await _client.PutAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/veiculos/00000000-0000-0000-0000-000000000001/fotos")]
    [InlineData("/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002/conteudo")]
    public async Task UsuarioSemVeiculosVisualizar_FotosBloqueadas(string rota)
    {
        var response = await _client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PATCH", "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002/principal")]
    [InlineData("DELETE", "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002")]
    public async Task UsuarioSemVeiculosEditar_MutacoesDeFotoBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.VeiculosVisualizar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemVeiculosEditar_UploadDeFotoBloqueado()
    {
        UsarPermissoes(Permissoes.VeiculosVisualizar);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]), "arquivo", "foto.jpg");
        var response = await _client.PostAsync(
            "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos",
            content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos")]
    [InlineData("/api/categorias-servico")]
    public async Task UsuarioSemServicosVisualizar_Recebe403(string rota)
    {
        var response = await _client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComServicosVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ServicosVisualizar);
        var response = await _client.GetAsync("/api/servicos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos", Permissoes.ServicosCriar)]
    [InlineData("/api/pacotes", Permissoes.PacotesCriar)]
    public async Task UsuarioSemPermissaoDeCriacao_Recebe403(string rota, string permissaoNecessaria)
    {
        UsarPermissoes(Permissoes.ServicosVisualizar, Permissoes.PacotesVisualizar);
        Assert.DoesNotContain(permissaoNecessaria, _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        var response = await _client.PostAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos/00000000-0000-0000-0000-000000000001", Permissoes.ServicosEditar)]
    [InlineData("/api/pacotes/00000000-0000-0000-0000-000000000001", Permissoes.PacotesEditar)]
    public async Task UsuarioSemPermissaoDeEdicao_Recebe403(string rota, string permissaoNecessaria)
    {
        UsarPermissoes(Permissoes.ServicosVisualizar, Permissoes.PacotesVisualizar);
        Assert.DoesNotContain(permissaoNecessaria, _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        var response = await _client.PutAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemPacotesVisualizar_Recebe403()
    {
        UsarPermissoes(Permissoes.ServicosVisualizar);
        var response = await _client.GetAsync("/api/pacotes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComPacotesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.PacotesVisualizar);
        var response = await _client.GetAsync("/api/pacotes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemAgendaVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/agenda/contexto");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemAgendaCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.AgendaVisualizar);
        var response = await _client.PostAsJsonAsync("/api/agendamentos", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/api/agendamentos/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/agendamentos/00000000-0000-0000-0000-000000000001/status")]
    [InlineData("PATCH", "/api/agendamentos/00000000-0000-0000-0000-000000000001/reagendar")]
    public async Task UsuarioSemAgendaEditar_AlteracoesBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.AgendaVisualizar, Permissoes.AgendaCriar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(new { }) };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrcamentosVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/orcamentos");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrcamentosCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar);
        var response = await _client.PostAsJsonAsync("/api/orcamentos", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrcamentoComItemValido_PassaPeloPipelineESalvaRascunho()
    {
        UsarPermissoes(Permissoes.OrcamentosCriar);
        var request = new SalvarOrcamentoRequest(
            _factory.ClienteId,
            _factory.VeiculoId,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            null,
            null,
            "À vista",
            0,
            0,
            [new(TipoItemOrcamentoContrato.Servico, _factory.ServicoId, null, null, 160m, 1, null)]);

        var response = await _client.PostAsJsonAsync("/api/orcamentos", request);
        var conteudo = await response.Content.ReadFromJsonAsync<RespostaApi<OrcamentoDetalheResponse>>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(conteudo?.Sucesso);
        Assert.Equal(160m, conteudo?.Resultado?.Total);
        Assert.Single(conteudo!.Resultado!.Itens);
    }

    [Fact]
    public async Task OrcamentoInvalido_RetornaDetalhesNoFormatoPadraoDaApi()
    {
        UsarPermissoes(Permissoes.OrcamentosCriar);
        var request = new SalvarOrcamentoRequest(
            Guid.Empty,
            Guid.Empty,
            null,
            default,
            null,
            null,
            null,
            -1,
            0,
            [new(TipoItemOrcamentoContrato.Servico, null, null, null, -1, 0, null)]);

        var response = await _client.PostAsJsonAsync("/api/orcamentos", request);
        var conteudo = await response.Content.ReadFromJsonAsync<RespostaApi<object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(conteudo?.Sucesso);
        Assert.Equal("validacao", conteudo?.Erro?.Codigo);
        Assert.Contains("ClienteId", conteudo!.Erro!.Detalhes!.Keys);
        Assert.Contains("Itens[0].ValorUnitario", conteudo.Erro.Detalhes.Keys);
        Assert.Contains("Itens[0].Quantidade", conteudo.Erro.Detalhes.Keys);
    }

    [Fact]
    public async Task PdfOrcamento_Autorizado_RetornaArquivoPdfValido()
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar);
        var response = await _client.GetAsync($"/api/orcamentos/{_factory.OrcamentoId}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Theory]
    [InlineData("PUT", "/api/orcamentos/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/emitir")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/aprovar")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/recusar")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/cancelar")]
    public async Task UsuarioSemOrcamentosEditar_AlteracoesBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar, Permissoes.OrcamentosCriar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(new { observacao = (string?)null }) };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrdemServicoVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/ordens-servico");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComOrdemServicoVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        var response = await _client.GetAsync("/api/ordens-servico");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrdemServicoCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        var response = await _client.PostAsJsonAsync("/api/ordens-servico", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/check-in", Permissoes.OrdemServicoEditar)]
    [InlineData("PUT", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/checklist", Permissoes.OrdemServicoEditar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/iniciar-execucao", Permissoes.OrdemServicoFinalizar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/finalizar-execucao", Permissoes.OrdemServicoFinalizar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/concluir", Permissoes.OrdemServicoFinalizar)]
    public async Task UsuarioSemPermissaoOperacionalDaOs_Recebe403(string metodo, string rota, string permissao)
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        Assert.DoesNotContain(permissao,
            _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota)
        {
            Content = JsonContent.Create(new { observacao = (string?)null })
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemFinanceiroVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/financeiro/resumo");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComFinanceiroVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.FinanceiroVisualizar);
        var response = await _client.GetAsync("/api/financeiro/resumo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/pagamentos", Permissoes.FinanceiroRegistrarPagamento)]
    [InlineData("POST", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/pagamentos/00000000-0000-0000-0000-000000000002/estornar", Permissoes.FinanceiroEstornarPagamento)]
    [InlineData("PATCH", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/vencimento", Permissoes.FinanceiroEditar)]
    public async Task UsuarioSemPermissaoFinanceiraDeMutacao_Recebe403(string metodo, string rota, string permissao)
    {
        UsarPermissoes(Permissoes.FinanceiroVisualizar);
        Assert.DoesNotContain(permissao,
            _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota)
        { Content = JsonContent.Create(new { }) };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private void UsarPermissoes(params string[] permissoes)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.PermissionsHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
    }

    private sealed class DetaraApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        public const string EmailLogin = "login@detara.local";
        public const string SenhaLogin = "senha-segura-login";
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly Dictionary<string, string?> _environmentBeforeTest = new();
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public Guid EmpresaOutroTenantId { get; } = Guid.NewGuid();
        public Guid ClienteId { get; private set; }
        public Guid ClienteOutroTenantId { get; private set; }
        public Guid VeiculoId { get; private set; }
        public Guid ServicoId { get; private set; }
        public Guid OrcamentoId { get; private set; }

        public DetaraApiFactory()
        {
            SetTestEnvironment("Jwt__Emissor", "Detara.Tests");
            SetTestEnvironment("Jwt__Audiencia", "Detara.Tests");
            SetTestEnvironment("Jwt__ChaveAssinatura", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            SetTestEnvironment("Jwt__ExpiracaoMinutos", "60");
            SetTestEnvironment("ConnectionStrings__DefaultConnection", "Data Source=unused");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DetaraDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<DetaraDbContext>>();
                services.RemoveAll<DetaraDbContext>();
                services.AddDbContext<DetaraDbContext>(options => options.UseSqlite(_connection));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.AddSingleton(new TestTenant(EmpresaId));
            });
        }

        public async Task InicializarBancoAsync()
        {
            using var scope = Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var systemContext = new DetaraDbContext(options, TestUserContext.Anonymous);
            await systemContext.Database.EnsureCreatedAsync();
            var empresa = new Empresa("Empresa Teste", "Empresa Teste Ltda", "12345678000190", "empresa-teste");
            typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, EmpresaId);
            var empresaOutroTenant = new Empresa(
                "Empresa Outro Tenant",
                "Empresa Outro Tenant Ltda",
                "98765432000198",
                "empresa-outro-tenant");
            typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!
                .SetValue(empresaOutroTenant, EmpresaOutroTenantId);
            systemContext.Empresas.AddRange(empresa, empresaOutroTenant);
            await systemContext.SaveChangesAsync();

            await using (var outroTenant = new DetaraDbContext(
                options,
                new TestUserContext(EmpresaOutroTenantId)))
            {
                var clienteOutroTenant = new Cliente(
                    EmpresaOutroTenantId,
                    "Cliente outro tenant",
                    TipoPessoa.PessoaFisica,
                    "52998224725",
                    null,
                    null,
                    null,
                    null,
                    null);
                outroTenant.Clientes.Add(clienteOutroTenant);
                await outroTenant.SaveChangesAsync();
                ClienteOutroTenantId = clienteOutroTenant.Id;
            }

            await using var tenantContext = new DetaraDbContext(options, new TestUserContext(EmpresaId));
            var perfilLogin = new Perfil(EmpresaId, "Administrador Login");
            tenantContext.Perfis.Add(perfilLogin);
            await tenantContext.SaveChangesAsync();
            var usuarioLogin = new Usuario(
                EmpresaId,
                perfilLogin.Id,
                "Usuário Login",
                EmailLogin,
                "hash-temporario");
            var senhaServico = scope.ServiceProvider.GetRequiredService<ISenhaServico>();
            usuarioLogin.AlterarSenhaHash(senhaServico.GerarHash(usuarioLogin, SenhaLogin));
            tenantContext.Usuarios.Add(usuarioLogin);
            var cliente = new Cliente(
                EmpresaId,
                "Cliente Teste",
                TipoPessoa.PessoaFisica,
                "52998224725",
                null,
                null,
                null,
                null,
                null);
            tenantContext.Clientes.Add(cliente);
            await tenantContext.SaveChangesAsync();
            ClienteId = cliente.Id;
            var veiculo = new Veiculo(EmpresaId, cliente.Id, "ABC1D23", "Honda", "Civic", null,
                2024, 2024, "Preto", 15000, null);
            tenantContext.Veiculos.Add(veiculo);
            var categoria = new CategoriaServico(EmpresaId, "Lavagem", null, 1);
            tenantContext.CategoriasServico.Add(categoria);
            await tenantContext.SaveChangesAsync();
            var servico = new Servico(EmpresaId, categoria.Id, "Lavagem técnica", null,
                TipoPrecificacao.APartirDe, 100m, 90, 1);
            tenantContext.Servicos.Add(servico);
            await tenantContext.SaveChangesAsync();
            VeiculoId = veiculo.Id;
            ServicoId = servico.Id;
            var usuarioId = Guid.NewGuid();
            var orcamento = new Orcamento(EmpresaId,
                new(cliente.Id, cliente.Nome, cliente.CpfCnpj, cliente.Telefone, veiculo.Id, "Honda Civic", "ABC1D23"),
                null, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), "Proposta comercial", null, "À vista", 0, 0,
                [new(TipoItemOrcamento.Servico, Guid.NewGuid(), "Lavagem técnica", null, TipoPrecificacao.APartirDe, 100m, 160m, 1, 1, null)], usuarioId);
            orcamento.Emitir(DateTime.UtcNow.Year, usuarioId);
            tenantContext.Orcamentos.Add(orcamento);
            await tenantContext.SaveChangesAsync();
            OrcamentoId = orcamento.Id;
        }

        public async Task<string> ObterNomeClienteGlobalAsync(Guid id) =>
            (await ObterClienteGlobalAsync(id)).Nome;

        public async Task AdicionarMembershipOutroTenantAsync(string senha)
        {
            using var scope = Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var context = new DetaraDbContext(
                options,
                new TestUserContext(EmpresaOutroTenantId));
            var perfil = new Perfil(EmpresaOutroTenantId, "Administrador Outro Tenant");
            context.Perfis.Add(perfil);
            await context.SaveChangesAsync();
            var usuario = new Usuario(
                EmpresaOutroTenantId,
                perfil.Id,
                "Usuário Outro Tenant",
                EmailLogin,
                "hash-temporario");
            var senhaServico = scope.ServiceProvider.GetRequiredService<ISenhaServico>();
            usuario.AlterarSenhaHash(senhaServico.GerarHash(usuario, senha));
            context.Usuarios.Add(usuario);
            await context.SaveChangesAsync();
        }

        public async Task DesativarEmpresaOutroTenantAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DetaraDbContext>();
            var empresa = await context.Empresas.SingleAsync(x => x.Id == EmpresaOutroTenantId);
            empresa.Desativar();
            await context.SaveChangesAsync();
        }

        public async Task<(Guid EmpresaId, bool EhAtivo, DateTime CriadoEmUtc)>
            ObterEstadoClienteGlobalAsync(Guid id)
        {
            var cliente = await ObterClienteGlobalAsync(id);
            return (cliente.EmpresaId, cliente.EhAtivo, cliente.CriadoEmUtc);
        }

        private async Task<Cliente> ObterClienteGlobalAsync(Guid id)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DetaraDbContext>();
            return await context.Clientes.IgnoreQueryFilters().SingleAsync(item => item.Id == id);
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
            foreach (var (key, value) in _environmentBeforeTest)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private void SetTestEnvironment(string key, string value)
        {
            _environmentBeforeTest[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed record TestTenant(Guid EmpresaId);

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestTenant tenant)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString()),
                new("empresa_id", tenant.EmpresaId.ToString()),
                new("name", "Usuário Teste")
            };
            var permissions = Request.Headers[PermissionsHeader].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            claims.AddRange(permissions.Select(permission => new Claim("permissao", permission)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class TestUserContext(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public static TestUserContext Anonymous { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
