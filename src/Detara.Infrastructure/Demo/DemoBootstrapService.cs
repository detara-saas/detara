using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Atendimento;
using Detara.Application.Catalogo;
using Detara.Application.Clientes;
using Detara.Application.Financeiro;
using Detara.Application.FluxoOperacional;
using Detara.Application.Notificacoes;
using Detara.Application.Onboarding;
using Detara.Application.Veiculos;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Detara.Infrastructure.Veiculos;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.Infrastructure.Demo;

public static class DemoBootstrapPolicy
{
    public const string Confirmacao = "--confirm-local-demo";
    public const string MensagemSomenteDevelopment =
        "Demo Bootstrap pode ser executado somente em Development.";

    public static void ExigirDevelopment(string? ambiente)
    {
        if (!string.Equals(ambiente, "Development", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(MensagemSomenteDevelopment);
        }
    }

    public static void ExigirConfirmacao(bool confirmacaoInformada)
    {
        if (!confirmacaoInformada)
        {
            throw new InvalidOperationException(
                $"Confirme a operação local com {Confirmacao}.");
        }
    }

    public static void ValidarSenha(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length is < 10 or > 256)
        {
            throw new ArgumentException(
                "A senha deve possuir entre 10 e 256 caracteres.",
                nameof(senha));
        }
    }
}

public sealed record DemoBootstrapStatus(
    bool Encontrada,
    Guid? EmpresaId,
    int Empresas,
    int Perfis,
    int Usuarios,
    int Clientes,
    int Veiculos,
    int Categorias,
    int Servicos,
    int Pacotes,
    int Agendamentos,
    int Orcamentos,
    int OrdensServico,
    int ContasReceber,
    int Pagamentos,
    int Notificacoes);

public sealed record DemoBootstrapResult(bool JaExistia, DemoBootstrapStatus Status);

public sealed class DemoBootstrapService(
    DbContextOptions<DetaraDbContext> options,
    IPasswordHasher<Usuario> passwordHasher,
    TimeProvider? timeProvider = null)
{
    public const string SlugEmpresa = "prime-detail-demo";
    public const string NomeEmpresa = "Prime Detail Estética Automotiva";
    public const string EmailAdministrador = "demo@detara.local";
    public const string FusoHorario = "America/Sao_Paulo";

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DemoBootstrapResult> CriarAsync(
        string senhaAdministrador,
        CancellationToken cancellationToken = default)
    {
        DemoBootstrapPolicy.ValidarSenha(senhaAdministrador);

        await using var sistema = CriarContexto(ContextoDemo.Anonimo);
        var existente = await sistema.Empresas
            .SingleOrDefaultAsync(empresa => empresa.Slug == SlugEmpresa, cancellationToken);
        if (existente is not null)
        {
            return new DemoBootstrapResult(true, await ObterStatusAsync(cancellationToken));
        }

        var empresa = new Empresa(
            NomeEmpresa,
            "Prime Detail Demo Ltda.",
            "99999999000199",
            SlugEmpresa,
            "contato@prime-detail.local",
            "1100000000",
            FusoHorario);
        await GarantirEmailExclusivoAsync(sistema, empresa.Id, cancellationToken);
        sistema.Empresas.Add(empresa);
        await sistema.SaveChangesAsync(cancellationToken);

        try
        {
            await ReconstruirCenarioAsync(empresa.Id, senhaAdministrador, limparAntes: false, cancellationToken);
        }
        catch
        {
            await RemoverEmpresaIncompletaAsync(empresa.Id, cancellationToken);
            throw;
        }

        return new DemoBootstrapResult(false, await ObterStatusAsync(cancellationToken));
    }

    public async Task<DemoBootstrapResult> ResetarAsync(
        string senhaAdministrador,
        CancellationToken cancellationToken = default)
    {
        DemoBootstrapPolicy.ValidarSenha(senhaAdministrador);

        await using var sistema = CriarContexto(ContextoDemo.Anonimo);
        var empresa = await sistema.Empresas
            .SingleOrDefaultAsync(item => item.Slug == SlugEmpresa, cancellationToken);
        if (empresa is null)
        {
            return await CriarAsync(senhaAdministrador, cancellationToken);
        }

        await GarantirEmailExclusivoAsync(sistema, empresa.Id, cancellationToken);
        empresa.AtualizarCadastro(
            NomeEmpresa,
            "Prime Detail Demo Ltda.",
            "99999999000199",
            "contato@prime-detail.local",
            "1100000000",
            FusoHorario,
            empresa.VersaoCadastro);
        empresa.Reativar();
        await sistema.SaveChangesAsync(cancellationToken);
        await ReconstruirCenarioAsync(empresa.Id, senhaAdministrador, limparAntes: true, cancellationToken);
        return new DemoBootstrapResult(false, await ObterStatusAsync(cancellationToken));
    }

    public async Task<DemoBootstrapStatus> ObterStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await using var sistema = CriarContexto(ContextoDemo.Anonimo);
        var empresa = await sistema.Empresas
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == SlugEmpresa, cancellationToken);
        if (empresa is null)
        {
            return new(false, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        await using var tenant = CriarContexto(new ContextoDemo(empresa.Id, Guid.NewGuid()));
        return new(
            true,
            empresa.Id,
            1,
            await tenant.Perfis.CountAsync(cancellationToken),
            await tenant.Usuarios.CountAsync(cancellationToken),
            await tenant.Clientes.CountAsync(cancellationToken),
            await tenant.Veiculos.CountAsync(cancellationToken),
            await tenant.CategoriasServico.CountAsync(cancellationToken),
            await tenant.Servicos.CountAsync(cancellationToken),
            await tenant.Pacotes.CountAsync(cancellationToken),
            await tenant.Agendamentos.CountAsync(cancellationToken),
            await tenant.Orcamentos.CountAsync(cancellationToken),
            await tenant.OrdensServico.CountAsync(cancellationToken),
            await tenant.ContasReceber.CountAsync(cancellationToken),
            await tenant.Pagamentos.CountAsync(cancellationToken),
            await tenant.NotificacoesEmail.CountAsync(cancellationToken));
    }

    private async Task ReconstruirCenarioAsync(
        Guid empresaId,
        string senhaAdministrador,
        bool limparAntes,
        CancellationToken cancellationToken)
    {
        var usuarioContexto = new ContextoDemo(empresaId, Guid.NewGuid());
        var db = CriarContexto(usuarioContexto);
        await using var provider = CriarProvider(db, usuarioContexto);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (limparAntes)
            {
                await LimparTenantAsync(db, empresaId, cancellationToken);
                db.ChangeTracker.Clear();
            }

            var admin = await CriarIdentidadesAsync(
                db,
                empresaId,
                senhaAdministrador,
                cancellationToken);
            usuarioContexto.DefinirUsuario(admin.Id);
            var sender = provider.GetRequiredService<ISender>();
            await PopularOperacaoAsync(sender, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Usuario> CriarIdentidadesAsync(
        DetaraDbContext db,
        Guid empresaId,
        string senhaAdministrador,
        CancellationToken cancellationToken)
    {
        var permissoes = await db.Permissoes.ToDictionaryAsync(
            item => item.Codigo,
            StringComparer.Ordinal,
            cancellationToken);
        foreach (var definicao in Permissoes.Definicoes)
        {
            if (!permissoes.ContainsKey(definicao.Codigo))
            {
                var permissao = new Permissao(definicao.Codigo, definicao.Descricao);
                db.Permissoes.Add(permissao);
                permissoes.Add(permissao.Codigo, permissao);
            }
        }

        var administrador = new Perfil(
            empresaId,
            "Administrador",
            "Perfil administrativo protegido com acesso integral ao tenant.",
            ehSistema: true);
        foreach (var permissao in permissoes.Values)
        {
            administrador.ConcederPermissao(permissao);
        }

        var recepcao = new Perfil(
            empresaId,
            "Recepção",
            "Atendimento ao cliente, agenda e preparação comercial.");
        Conceder(recepcao, permissoes,
            Permissoes.ClientesVisualizar,
            Permissoes.ClientesCriar,
            Permissoes.ClientesEditar,
            Permissoes.VeiculosVisualizar,
            Permissoes.VeiculosCriar,
            Permissoes.VeiculosEditar,
            Permissoes.ServicosVisualizar,
            Permissoes.AgendaVisualizar,
            Permissoes.AgendaCriar,
            Permissoes.AgendaEditar,
            Permissoes.OrcamentosVisualizar,
            Permissoes.OrcamentosCriar,
            Permissoes.OrcamentosEditar,
            Permissoes.OrdemServicoVisualizar);
        var operacao = new Perfil(
            empresaId,
            "Operação",
            "Execução de ordens de serviço e checklist operacional.");
        Conceder(operacao, permissoes,
            Permissoes.ClientesVisualizar,
            Permissoes.VeiculosVisualizar,
            Permissoes.ServicosVisualizar,
            Permissoes.AgendaVisualizar,
            Permissoes.OrdemServicoVisualizar,
            Permissoes.OrdemServicoEditar,
            Permissoes.OrdemServicoFinalizar,
            Permissoes.ConfiguracoesVisualizar);

        db.Perfis.AddRange(administrador, recepcao, operacao);
        await db.SaveChangesAsync(cancellationToken);

        var admin = new Usuario(
            empresaId,
            administrador.Id,
            "Administrador Prime Detail",
            EmailAdministrador,
            "hash-pendente");
        admin.AlterarSenhaHash(passwordHasher.HashPassword(admin, senhaAdministrador));

        var camila = CriarUsuarioSecundario(
            empresaId,
            recepcao.Id,
            "Camila — Recepção",
            "camila.recepcao@prime-detail.local");
        var rafael = CriarUsuarioSecundario(
            empresaId,
            operacao.Id,
            "Rafael — Operação",
            "rafael.operacao@prime-detail.local");
        db.Usuarios.AddRange(admin, camila, rafael);
        await db.SaveChangesAsync(cancellationToken);
        return admin;
    }

    private Usuario CriarUsuarioSecundario(
        Guid empresaId,
        Guid perfilId,
        string nome,
        string email)
    {
        var usuario = new Usuario(empresaId, perfilId, nome, email, "hash-pendente");
        var segredoAleatorio = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        usuario.AlterarSenhaHash(passwordHasher.HashPassword(usuario, segredoAleatorio));
        usuario.DesativarAcesso(usuario.Versao);
        return usuario;
    }

    private async Task PopularOperacaoAsync(ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new AtualizarChecklistModeloCommand(
            "Inspeção de entrada Prime Detail",
            "Checklist sintético para demonstração local.",
            [
                "Conferir riscos e avarias aparentes",
                "Conferir objetos deixados no interior",
                "Conferir rodas e pneus",
                "Registrar observações de acabamento"
            ]), cancellationToken);
        await sender.Send(new AtualizarConfiguracaoOperacionalCommand(
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado), cancellationToken);
        await sender.Send(new AtualizarConfiguracaoNotificacaoCommand(false, null), cancellationToken);

        var categorias = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var item in new[]
        {
            ("Lavagem", "Cuidados técnicos de lavagem e descontaminação.", 1),
            ("Higienização", "Limpeza profunda do interior.", 2),
            ("Polimento", "Correção e refinamento de pintura.", 3),
            ("Proteção", "Proteções de pintura e superfícies.", 4),
            ("Cuidados Especiais", "Serviços pontuais de recuperação estética.", 5)
        })
        {
            var categoria = await sender.Send(
                new CriarCategoriaServicoCommand(item.Item1, item.Item2, item.Item3),
                cancellationToken);
            categorias.Add(item.Item1, categoria.Id);
        }

        var servicos = new Dictionary<string, ServicoDetalheResultado>(StringComparer.Ordinal);
        foreach (var item in new[]
        {
            new ServicoDemo("Lavagem", "Lavagem Técnica", TipoPrecificacao.Fixo, 120m, 90),
            new ServicoDemo("Lavagem", "Lavagem Detalhada", TipoPrecificacao.Fixo, 190m, 150),
            new ServicoDemo("Higienização", "Higienização Interna", TipoPrecificacao.APartirDe, 350m, 240),
            new ServicoDemo("Polimento", "Polimento Comercial", TipoPrecificacao.APartirDe, 450m, 360),
            new ServicoDemo("Polimento", "Polimento Técnico", TipoPrecificacao.APartirDe, 800m, 480),
            new ServicoDemo("Proteção", "Vitrificação de Pintura", TipoPrecificacao.APartirDe, 1200m, 480),
            new ServicoDemo("Cuidados Especiais", "Revitalização de Faróis", TipoPrecificacao.Fixo, 180m, 120),
            new ServicoDemo("Cuidados Especiais", "Limpeza Técnica de Motor", TipoPrecificacao.Fixo, 150m, 90),
            new ServicoDemo("Proteção", "Proteção de Plásticos", TipoPrecificacao.Fixo, 160m, 120),
            new ServicoDemo("Lavagem", "Descontaminação de Pintura", TipoPrecificacao.APartirDe, 220m, 150)
        })
        {
            var servico = await sender.Send(new CriarServicoCommand(
                categorias[item.Categoria],
                item.Nome,
                "Serviço sintético do cenário Prime Detail.",
                item.TipoPrecificacao,
                item.Preco,
                item.DuracaoMinutos,
                servicos.Count + 1), cancellationToken);
            servicos.Add(item.Nome, servico);
        }

        var clientes = new List<ClienteVeiculoDemo>();
        var dadosClientes = new[]
        {
            new ClienteDemo("André Moreira", "andre.moreira@example.com", "1100000001", "Honda", "Civic", "DMO1A01", "Prata", 38200),
            new ClienteDemo("Camila Duarte", "camila.duarte@example.com", "1100000002", "Toyota", "Corolla", "DMO1A02", "Branco", 27400),
            new ClienteDemo("Bruno Ferraz", "bruno.ferraz@example.com", "1100000003", "Volkswagen", "T-Cross", "DMO1A03", "Cinza", 21900),
            new ClienteDemo("Mariana Lopes", "mariana.lopes@example.com", "1100000004", "Jeep", "Compass", "DMO1A04", "Preto", 43100),
            new ClienteDemo("Eduardo Nascimento", "eduardo.nascimento@example.com", "1100000005", "BMW", "320i", "DMO1A05", "Azul", 18700),
            new ClienteDemo("Fernanda Ribeiro", "fernanda.ribeiro@example.com", "1100000006", "Chevrolet", "Tracker", "DMO1A06", "Vermelho", 30500),
            new ClienteDemo("Lucas Barreto", "lucas.barreto@example.com", "1100000007", "Volkswagen", "Golf", "DMO1A07", "Branco", 59200),
            new ClienteDemo("Juliana Prado", "juliana.prado@example.com", "1100000008", "Audi", "A3", "DMO1A08", "Cinza", 24600),
            new ClienteDemo("Rafael Marins", "rafael.marins@example.com", "1100000009", "Sea-Doo", "GTX 300", null, "Amarelo", 46,
                TipoVeiculo.MotoAquatica, "DEMO-JET-01")
        };
        foreach (var item in dadosClientes)
        {
            var cliente = await sender.Send(new CriarClienteCommand(
                item.Nome,
                nameof(TipoPessoa.PessoaFisica),
                null,
                item.Telefone,
                item.Telefone,
                item.Email,
                null,
                "Cadastro totalmente sintético para demonstração local."), cancellationToken);
            var veiculo = await sender.Send(new CriarVeiculoCommand(
                cliente.Id,
                item.Tipo,
                item.Placa,
                item.IdentificacaoAlternativa,
                item.Marca,
                item.Modelo,
                null,
                2022,
                2023,
                item.Cor,
                item.Quilometragem,
                "Veículo sintético do cenário Prime Detail."), cancellationToken);
            clientes.Add(new(cliente, veiculo));
        }

        var hoje = ObterHojeLocal();
        var agendamentos = new List<AgendamentoDetalheVisualizacao>();
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[0], servicos["Lavagem Detalhada"], hoje.AddDays(-1), 10, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[3], servicos["Higienização Interna"], hoje, 9, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[2], servicos["Lavagem Técnica"], hoje, 15, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[4], servicos["Vitrificação de Pintura"], hoje.AddDays(1), 9, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[5], servicos["Polimento Comercial"], hoje.AddDays(-1), 13, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[6], servicos["Descontaminação de Pintura"], hoje.AddDays(4), 10, cancellationToken));
        agendamentos.Add(await CriarAgendamentoAsync(sender, clientes[7], servicos["Proteção de Plásticos"], hoje.AddDays(-1), 16, cancellationToken));

        await AlterarStatusAgendaAsync(sender, agendamentos[2], StatusAgendamento.Confirmado, cancellationToken);
        await AlterarStatusAgendaAsync(sender, agendamentos[3], StatusAgendamento.Confirmado, cancellationToken);
        await sender.Send(new AlterarStatusAgendaOperacionalCommand(
            agendamentos[6].Agendamento.Id,
            StatusAgendamento.Cancelado,
            "Cancelamento sintético para demonstrar histórico."), cancellationToken);

        _ = await CriarOrcamentoAsync(sender, clientes[1], servicos["Higienização Interna"], hoje, "rascunho", cancellationToken);
        var aprovadoVitrificacao = await CriarOrcamentoAsync(sender, clientes[4], servicos["Vitrificação de Pintura"], hoje, "aprovado", cancellationToken, agendamentos[3].Agendamento.Id);
        _ = await CriarOrcamentoAsync(sender, clientes[5], servicos["Polimento Comercial"], hoje, "recusado", cancellationToken);
        var aprovadoLavagem = await CriarOrcamentoAsync(sender, clientes[0], servicos["Lavagem Detalhada"], hoje, "aprovado", cancellationToken, agendamentos[0].Agendamento.Id);

        var osExecucao = await sender.Send(new CriarOrdemServicoCommand(
            null,
            agendamentos[1].Agendamento.Id,
            clientes[3].Cliente.Id,
            clientes[3].Veiculo.Id,
            240,
            0,
            0,
            "Atendimento originado da agenda da demonstração.",
            [ItemOs(servicos["Higienização Interna"])]), cancellationToken);
        osExecucao = await sender.Send(new RealizarCheckInCommand(
            osExecucao.OrdemServico.Id,
            clientes[3].Veiculo.Quilometragem,
            "Entrada conferida; cenário sintético."), cancellationToken);
        var respostasParciais = osExecucao.OrdemServico.Checklist!.Itens.Take(2)
            .Select(item => new RespostaChecklistSnapshot(
                item.Id,
                RespostaChecklistOrdemServico.Conforme,
                null))
            .ToArray();
        await sender.Send(new AtualizarChecklistOrdemServicoCommand(
            osExecucao.OrdemServico.Id,
            respostasParciais), cancellationToken);
        await sender.Send(new TransicaoOrdemServicoCommand(
            osExecucao.OrdemServico.Id,
            "Higienização interna em execução."), cancellationToken);

        var osMista = await sender.Send(new CriarOrdemServicoCommand(
            aprovadoVitrificacao.Orcamento.Id,
            agendamentos[3].Agendamento.Id,
            null,
            null,
            null,
            0,
            0,
            null,
            []), cancellationToken);
        osMista = await PrepararEIniciarAsync(sender, osMista, 18700, cancellationToken);
        await sender.Send(new FinalizarExecucaoOrdemServicoCommand(
            osMista.OrdemServico.Id,
            "Vitrificação concluída; aguardando retirada."), cancellationToken);
        var contaMista = await ExigirContaAsync(sender, osMista.OrdemServico.Id, cancellationToken);
        await sender.Send(new RegistrarPagamentoCommand(
            contaMista,
            FormaPagamento.Pix,
            600m,
            0,
            null,
            "Primeira parte do pagamento sintético.",
            ObterAgoraLocal()), cancellationToken);
        await sender.Send(new RegistrarPagamentoCommand(
            contaMista,
            FormaPagamento.CartaoCredito,
            600m,
            18m,
            3,
            "Segunda parte do pagamento sintético.",
            ObterAgoraLocal()), cancellationToken);

        var osConcluida = await sender.Send(new CriarOrdemServicoCommand(
            aprovadoLavagem.Orcamento.Id,
            agendamentos[0].Agendamento.Id,
            null,
            null,
            null,
            0,
            0,
            null,
            []), cancellationToken);
        osConcluida = await PrepararEIniciarAsync(sender, osConcluida, 38200, cancellationToken);
        await sender.Send(new FinalizarExecucaoOrdemServicoCommand(
            osConcluida.OrdemServico.Id,
            "Lavagem detalhada concluída."), cancellationToken);
        await sender.Send(new ConcluirOrdemServicoCommand(
            osConcluida.OrdemServico.Id,
            "Veículo retirado no cenário sintético."), cancellationToken);
        var contaPaga = await ExigirContaAsync(sender, osConcluida.OrdemServico.Id, cancellationToken);
        await sender.Send(new RegistrarPagamentoCommand(
            contaPaga,
            FormaPagamento.Pix,
            190m,
            0,
            null,
            "Pagamento integral sintético.",
            ObterAgoraLocal()), cancellationToken);

        var osPendente = await sender.Send(new CriarOrdemServicoCommand(
            null,
            agendamentos[4].Agendamento.Id,
            clientes[5].Cliente.Id,
            clientes[5].Veiculo.Id,
            360,
            0,
            0,
            "Autorização direta sintética.",
            [ItemOs(servicos["Polimento Comercial"])]), cancellationToken);
        osPendente = await PrepararEIniciarAsync(sender, osPendente, 30500, cancellationToken);
        await sender.Send(new FinalizarExecucaoOrdemServicoCommand(
            osPendente.OrdemServico.Id,
            "Polimento concluído; recebível pendente."), cancellationToken);
    }

    private async Task<AgendamentoDetalheVisualizacao> CriarAgendamentoAsync(
        ISender sender,
        ClienteVeiculoDemo cliente,
        ServicoDetalheResultado servico,
        DateOnly data,
        int hora,
        CancellationToken cancellationToken) =>
        await sender.Send(new CriarAgendamentoCommand(
            cliente.Cliente.Id,
            cliente.Veiculo.Id,
            data.ToDateTime(new TimeOnly(hora, 0), DateTimeKind.Unspecified),
            servico.DuracaoEstimadaMinutos ?? 90,
            "Atendimento sintético para demonstração.",
            "Criado pelo Demo Bootstrap local.",
            [new ItemAgendamentoEntrada(TipoItemAgendamento.Servico, servico.Id)]),
            cancellationToken);

    private static Task<AgendamentoDetalheVisualizacao> AlterarStatusAgendaAsync(
        ISender sender,
        AgendamentoDetalheVisualizacao agendamento,
        StatusAgendamento status,
        CancellationToken cancellationToken) =>
        sender.Send(new AlterarStatusAgendaOperacionalCommand(
            agendamento.Agendamento.Id,
            status,
            null), cancellationToken);

    private static async Task<OrcamentoDetalheVisualizacao> CriarOrcamentoAsync(
        ISender sender,
        ClienteVeiculoDemo cliente,
        ServicoDetalheResultado servico,
        DateOnly hoje,
        string estado,
        CancellationToken cancellationToken,
        Guid? agendamentoId = null)
    {
        var orcamento = await sender.Send(new CriarOrcamentoCommand(
            cliente.Cliente.Id,
            cliente.Veiculo.Id,
            agendamentoId,
            hoje.AddDays(7),
            "Proposta sintética para apresentação.",
            "Criada pelo Demo Bootstrap local.",
            "Valores demonstrativos; cenário sem validade comercial.",
            0,
            0,
            [new ItemOrcamentoEntrada(
                TipoItemOrcamento.Servico,
                servico.Id,
                null,
                null,
                servico.PrecoBase ?? 0,
                1,
                null)]), cancellationToken);
        if (estado == "rascunho")
        {
            return orcamento;
        }

        orcamento = await sender.Send(new EmitirOrcamentoCommand(
            orcamento.Orcamento.Id,
            "Proposta emitida no cenário sintético."), cancellationToken);
        return estado switch
        {
            "aprovado" => await sender.Send(new AprovarOrcamentoCommand(
                orcamento.Orcamento.Id,
                "Aprovação sintética registrada para a demonstração."), cancellationToken),
            "recusado" => await sender.Send(new RecusarOrcamentoCommand(
                orcamento.Orcamento.Id,
                "Cliente fictício optou por não realizar neste momento."), cancellationToken),
            _ => orcamento
        };
    }

    private static ItemOrdemServicoEntrada ItemOs(ServicoDetalheResultado servico) => new(
        TipoItemOrcamento.Servico,
        servico.Id,
        null,
        null,
        servico.PrecoBase ?? 0,
        1,
        null);

    private static async Task<OrdemServicoDetalheVisualizacao> PrepararEIniciarAsync(
        ISender sender,
        OrdemServicoDetalheVisualizacao ordem,
        int quilometragem,
        CancellationToken cancellationToken)
    {
        ordem = await sender.Send(new RealizarCheckInCommand(
            ordem.OrdemServico.Id,
            quilometragem,
            "Check-in sintético conferido."), cancellationToken);
        var respostas = ordem.OrdemServico.Checklist!.Itens
            .Select(item => new RespostaChecklistSnapshot(
                item.Id,
                RespostaChecklistOrdemServico.Conforme,
                null))
            .ToArray();
        await sender.Send(new AtualizarChecklistOrdemServicoCommand(
            ordem.OrdemServico.Id,
            respostas), cancellationToken);
        return await sender.Send(new TransicaoOrdemServicoCommand(
            ordem.OrdemServico.Id,
            "Execução iniciada no cenário sintético."), cancellationToken);
    }

    private static async Task<Guid> ExigirContaAsync(
        ISender sender,
        Guid ordemServicoId,
        CancellationToken cancellationToken) =>
        await sender.Send(new ObterContaReceberPorOrdemServicoQuery(ordemServicoId), cancellationToken)
        ?? throw new InvalidOperationException("A conta a receber esperada não foi criada.");

    private DateOnly ObterHojeLocal() => DateOnly.FromDateTime(ObterAgoraLocal());

    private DateTime ObterAgoraLocal()
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(FusoHorario);
        return TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), fuso).DateTime;
    }

    private ServiceProvider CriarProvider(DetaraDbContext db, ContextoDemo usuario)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarApplication();
        services.AddSingleton<IUsuarioContexto>(usuario);
        services.AddSingleton(db);
        services.AddSingleton<IClientesRepositorio, ClientesRepositorio>();
        services.AddSingleton<IVeiculosRepositorio, VeiculosRepositorio>();
        services.AddSingleton<ICategoriasServicoRepositorio, CategoriasServicoRepositorio>();
        services.AddSingleton<IServicosRepositorio, ServicosRepositorio>();
        services.AddSingleton<IPacotesRepositorio, PacotesRepositorio>();
        services.AddSingleton<IAgendaRepositorio, AgendaRepositorio>();
        services.AddSingleton<IClientesAgendaConsulta, ClientesAgendaConsulta>();
        services.AddSingleton<ICatalogoAgendaConsulta, CatalogoAgendaConsulta>();
        services.AddSingleton<IFusoHorarioEmpresaConsulta, FusoHorarioEmpresaConsulta>();
        services.AddSingleton<IOrcamentosRepositorio, OrcamentosRepositorio>();
        services.AddSingleton<IOrdensServicoRepositorio, OrdensServicoRepositorio>();
        services.AddSingleton<IClientesAtendimentoConsulta, ClientesAtendimentoConsulta>();
        services.AddSingleton<ICatalogoAtendimentoConsulta, CatalogoAtendimentoConsulta>();
        services.AddSingleton<IAgendaAtendimentoIntegracao, AgendaAtendimentoIntegracao>();
        services.AddSingleton<IPlataformaAtendimentoConsulta, PlataformaAtendimentoConsulta>();
        services.AddSingleton<IConfiguracoesOperacionaisRepositorio, ConfiguracoesOperacionaisRepositorio>();
        services.AddSingleton<IFinanceiroRepositorio, FinanceiroRepositorio>();
        services.AddSingleton<IPlataformaFinanceiroConsulta, PlataformaFinanceiroConsulta>();
        services.AddSingleton<INotificacoesRepositorio, NotificacoesRepositorio>();
        services.AddSingleton<IPlataformaNotificacoesConsulta, PlataformaNotificacoesConsulta>();
        services.AddSingleton<IClientesNotificacoesConsulta, ClientesNotificacoesConsulta>();
        services.AddSingleton<IAtendimentoNotificacoesConsulta, AtendimentoNotificacoesConsulta>();
        services.AddSingleton<IRenderizadorTemplateEmail, RenderizadorTemplateEmail>();
        services.AddSingleton<IPlataformaOnboardingConsulta, PlataformaOnboardingConsulta>();
        services.AddSingleton<IAtendimentoOnboardingConsulta, AtendimentoOnboardingConsulta>();
        services.AddSingleton<ICatalogoOnboardingConsulta, CatalogoOnboardingConsulta>();
        services.AddSingleton<IClientesOnboardingConsulta, ClientesOnboardingConsulta>();
        services.AddSingleton<IAgendaOnboardingConsulta, AgendaOnboardingConsulta>();
        return services.BuildServiceProvider();
    }

    private static async Task LimparTenantAsync(
        DetaraDbContext db,
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        await db.TentativasNotificacaoEmail.ExecuteDeleteAsync(cancellationToken);
        await db.NotificacoesEmail.ExecuteDeleteAsync(cancellationToken);
        await db.TemplatesEmailEmpresa.ExecuteDeleteAsync(cancellationToken);
        await db.ConfiguracoesNotificacaoEmpresa.ExecuteDeleteAsync(cancellationToken);
        await db.Pagamentos.ExecuteDeleteAsync(cancellationToken);
        await db.ContasReceber.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServicoChecklistItens.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServicoChecklists.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServicoFotos.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServicoHistoricosStatus.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServicoItens.ExecuteDeleteAsync(cancellationToken);
        await db.OrdensServico.ExecuteDeleteAsync(cancellationToken);
        await db.OrcamentosHistoricosStatus.ExecuteDeleteAsync(cancellationToken);
        await db.OrcamentosItens.ExecuteDeleteAsync(cancellationToken);
        await db.Orcamentos.ExecuteDeleteAsync(cancellationToken);
        await db.AgendamentosItens.ExecuteDeleteAsync(cancellationToken);
        await db.Agendamentos.ExecuteDeleteAsync(cancellationToken);
        await db.VeiculosFotos.ExecuteDeleteAsync(cancellationToken);
        await db.PacotesServicos.ExecuteDeleteAsync(cancellationToken);
        await db.Pacotes.ExecuteDeleteAsync(cancellationToken);
        await db.Servicos.ExecuteDeleteAsync(cancellationToken);
        await db.CategoriasServico.ExecuteDeleteAsync(cancellationToken);
        await db.Veiculos.ExecuteDeleteAsync(cancellationToken);
        await db.Clientes.ExecuteDeleteAsync(cancellationToken);
        await db.ChecklistModeloItens.ExecuteDeleteAsync(cancellationToken);
        await db.ChecklistModelos.ExecuteDeleteAsync(cancellationToken);
        await db.ConfiguracoesOperacionaisAtendimento.ExecuteDeleteAsync(cancellationToken);
        await db.UsuariosPaginasFavoritas.ExecuteDeleteAsync(cancellationToken);
        await db.UsuariosPreferencias.ExecuteDeleteAsync(cancellationToken);
        await db.ConvitesAdministradoresEmpresa
            .Where(item => item.EmpresaId == empresaId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.Usuarios.ExecuteDeleteAsync(cancellationToken);
        await db.Perfis.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task GarantirEmailExclusivoAsync(
        DetaraDbContext sistema,
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        var reutilizado = await sistema.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(
                usuario => usuario.EmpresaId != empresaId && usuario.Email == EmailAdministrador,
                cancellationToken);
        if (reutilizado)
        {
            throw new InvalidOperationException(
                $"O e-mail local {EmailAdministrador} já pertence a outro tenant.");
        }
    }

    private async Task RemoverEmpresaIncompletaAsync(
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        await using var db = CriarContexto(ContextoDemo.Anonimo);
        var empresa = await db.Empresas.SingleOrDefaultAsync(
            item => item.Id == empresaId && item.Slug == SlugEmpresa,
            cancellationToken);
        if (empresa is null)
        {
            return;
        }

        db.Empresas.Remove(empresa);
        await db.SaveChangesAsync(cancellationToken);
    }

    private DetaraDbContext CriarContexto(IUsuarioContexto usuarioContexto) =>
        new(options, usuarioContexto);

    private static void Conceder(
        Perfil perfil,
        IReadOnlyDictionary<string, Permissao> permissoes,
        params string[] codigos)
    {
        foreach (var codigo in codigos)
        {
            perfil.ConcederPermissao(permissoes[codigo]);
        }
    }

    private sealed record ServicoDemo(
        string Categoria,
        string Nome,
        TipoPrecificacao TipoPrecificacao,
        decimal Preco,
        int DuracaoMinutos);

    private sealed record ClienteDemo(
        string Nome,
        string Email,
        string Telefone,
        string Marca,
        string Modelo,
        string? Placa,
        string Cor,
        int Quilometragem,
        TipoVeiculo Tipo = TipoVeiculo.Carro,
        string? IdentificacaoAlternativa = null);

    private sealed record ClienteVeiculoDemo(
        ClienteDetalheResultado Cliente,
        VeiculoDetalheResultado Veiculo);

    private sealed class ContextoDemo(Guid empresaId, Guid usuarioId) : IUsuarioContexto
    {
        public static ContextoDemo Anonimo { get; } = new(Guid.Empty, Guid.Empty);
        public Guid UsuarioId { get; private set; } = usuarioId;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;

        public void DefinirUsuario(Guid id) => UsuarioId = id;
    }
}
