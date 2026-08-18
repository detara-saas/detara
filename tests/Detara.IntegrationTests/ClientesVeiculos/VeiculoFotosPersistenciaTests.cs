using Detara.Application.Abstracoes;
using Detara.Application.Clientes;
using Detara.Domain.Clientes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.ClientesVeiculos;

public sealed class VeiculoFotosPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly StorageMemoria _storage = new();
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA;
    private Guid _empresaB;
    private Guid _veiculoA;
    private Guid _veiculoB;
    private readonly Guid _usuarioA = Guid.NewGuid();
    private readonly Guid _usuarioB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b");
        _empresaA = empresaA.Id;
        _empresaB = empresaB.Id;
        await using (var sistema = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo))
        {
            await sistema.Database.EnsureCreatedAsync();
            sistema.Empresas.AddRange(empresaA, empresaB);
            await sistema.SaveChangesAsync();
        }

        _veiculoA = await CriarVeiculoAsync(_empresaA, "ABC1D23");
        _veiculoB = await CriarVeiculoAsync(_empresaB, "XYZ9Z99");
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task PrimeiraFotoViraPrincipalESegundaNaoSubstitui()
    {
        await using var context = Contexto(_empresaA, _usuarioA);
        var primeira = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "primeira.txt");
        context.ChangeTracker.Clear();
        var segunda = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "segunda.png", ImagemPng());

        Assert.True(primeira.EhPrincipal);
        Assert.False(segunda.EhPrincipal);
        Assert.EndsWith(".jpg", _storage.Chaves.Single(chave => chave.Contains(primeira.Id.ToString("N"))));
        Assert.EndsWith(".png", _storage.Chaves.Single(chave => chave.Contains(segunda.Id.ToString("N"))));
    }

    [Fact]
    public async Task DefinirNovaPrincipal_DesmarcaAnterior()
    {
        await using var context = Contexto(_empresaA, _usuarioA);
        var primeira = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "primeira.jpg");
        context.ChangeTracker.Clear();
        var segunda = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "segunda.jpg");
        context.ChangeTracker.Clear();

        await new DefinirFotoPrincipalVeiculoHandler(new VeiculoFotosRepositorio(context))
            .Handle(new DefinirFotoPrincipalVeiculoCommand(_veiculoA, segunda.Id), default);
        context.ChangeTracker.Clear();

        var fotos = await context.VeiculosFotos.OrderBy(item => item.CriadoEmUtc).ToArrayAsync();
        Assert.False(fotos.Single(item => item.Id == primeira.Id).EhPrincipal);
        Assert.True(fotos.Single(item => item.Id == segunda.Id).EhPrincipal);
        Assert.Single(fotos, item => item.EhPrincipal);
    }

    [Fact]
    public async Task RemoverPrincipal_PromoveFotoMaisAntigaRestante()
    {
        await using var context = Contexto(_empresaA, _usuarioA);
        var primeira = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "primeira.jpg");
        context.ChangeTracker.Clear();
        var segunda = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "segunda.jpg");
        context.ChangeTracker.Clear();

        await new ExcluirFotoVeiculoHandler(new VeiculoFotosRepositorio(context), _storage)
            .Handle(new ExcluirFotoVeiculoCommand(_veiculoA, primeira.Id), default);
        context.ChangeTracker.Clear();

        var restante = Assert.Single(await context.VeiculosFotos.ToArrayAsync());
        Assert.Equal(segunda.Id, restante.Id);
        Assert.True(restante.EhPrincipal);
        Assert.DoesNotContain(_storage.Chaves, chave => chave.Contains(primeira.Id.ToString("N")));
    }

    [Fact]
    public async Task BancoArmazenaMetadadosENaoBinario()
    {
        await using var context = Contexto(_empresaA, _usuarioA);
        var enviada = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "foto-original.jpg");
        context.ChangeTracker.Clear();

        var foto = await context.VeiculosFotos.SingleAsync();
        Assert.Equal(enviada.Id, foto.Id);
        Assert.Equal("foto-original.jpg", foto.NomeOriginal);
        Assert.Equal("image/jpeg", foto.ContentType);
        Assert.Equal(ImagemJpeg().Length, foto.TamanhoBytes);
        Assert.DoesNotContain(typeof(VeiculoFoto).GetProperties(), propriedade => propriedade.PropertyType == typeof(byte[]));
        Assert.DoesNotContain(":\\", foto.ChaveStorage);
    }

    [Fact]
    public async Task EmpresaA_NaoListaBaixaExcluiOuDefinePrincipalDaEmpresaB()
    {
        await using (var contextB = Contexto(_empresaB, _usuarioB))
        {
            await Enviar(contextB, _empresaB, _usuarioB, _veiculoB, "empresa-b.jpg");
        }

        await using var contextA = Contexto(_empresaA, _usuarioA);
        var repositorioA = new VeiculoFotosRepositorio(contextA);
        await using var leituraB = Contexto(_empresaB, _usuarioB);
        var fotoB = await leituraB.VeiculosFotos.AsNoTracking().SingleAsync();

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new ListarFotosVeiculoHandler(repositorioA).Handle(new ListarFotosVeiculoQuery(_veiculoB), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new ObterConteudoVeiculoFotoHandler(repositorioA, _storage).Handle(
                new ObterConteudoVeiculoFotoQuery(_veiculoB, fotoB.Id), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new ExcluirFotoVeiculoHandler(repositorioA, _storage).Handle(
                new ExcluirFotoVeiculoCommand(_veiculoB, fotoB.Id), default));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            new DefinirFotoPrincipalVeiculoHandler(repositorioA).Handle(
                new DefinirFotoPrincipalVeiculoCommand(_veiculoB, fotoB.Id), default));
    }

    [Fact]
    public async Task UploadParaVeiculoDeOutroTenant_EhRejeitadoAntesDoStorage()
    {
        await using var contextA = Contexto(_empresaA, _usuarioA);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            Enviar(contextA, _empresaA, _usuarioA, _veiculoB, "tentativa.jpg"));

        Assert.Equal(0, _storage.QuantidadeSalvamentos);
        Assert.Empty(_storage.Chaves);
    }

    [Fact]
    public async Task VeiculoInativo_PermiteConsultaMasBloqueiaAlteracoes()
    {
        await using var context = Contexto(_empresaA, _usuarioA);
        var foto = await Enviar(context, _empresaA, _usuarioA, _veiculoA, "historica.jpg");
        context.ChangeTracker.Clear();
        var veiculo = await context.Veiculos.SingleAsync(item => item.Id == _veiculoA);
        veiculo.Desativar();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repositorio = new VeiculoFotosRepositorio(context);

        Assert.Single(await new ListarFotosVeiculoHandler(repositorio)
            .Handle(new ListarFotosVeiculoQuery(_veiculoA), default));
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            new DefinirFotoPrincipalVeiculoHandler(repositorio).Handle(
                new DefinirFotoPrincipalVeiculoCommand(_veiculoA, foto.Id), default));
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            new ExcluirFotoVeiculoHandler(repositorio, _storage).Handle(
                new ExcluirFotoVeiculoCommand(_veiculoA, foto.Id), default));
    }

    [Fact]
    public async Task FalhaAoPersistirMetadata_TentaLimparArquivoSalvo()
    {
        var empresaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var veiculo = new Veiculo(
            empresaId,
            Guid.NewGuid(),
            "ABC1D23",
            "Honda",
            "Civic",
            null,
            2024,
            2024,
            null,
            null,
            null);
        var storage = new StorageMemoria();
        var repositorio = new RepositorioComFalha(veiculo);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new EnviarFotoVeiculoHandler(
                new UsuarioContextoTeste(empresaId, usuarioId),
                repositorio,
                storage)
                .Handle(
                    new EnviarFotoVeiculoCommand(
                        veiculo.Id,
                        "foto.jpg",
                        ImagemJpeg().Length,
                        new MemoryStream(ImagemJpeg())),
                    default));

        Assert.Equal(1, storage.QuantidadeSalvamentos);
        Assert.Equal(1, storage.QuantidadeExclusoes);
        Assert.Empty(storage.Chaves);
    }

    private async Task<VeiculoFotoVisualizacao> Enviar(
        DetaraDbContext context,
        Guid empresaId,
        Guid usuarioId,
        Guid veiculoId,
        string nome,
        byte[]? bytes = null)
    {
        bytes ??= ImagemJpeg();
        return await new EnviarFotoVeiculoHandler(
            new UsuarioContextoTeste(empresaId, usuarioId),
            new VeiculoFotosRepositorio(context),
            _storage)
            .Handle(
                new EnviarFotoVeiculoCommand(
                    veiculoId,
                    nome,
                    bytes.Length,
                    new MemoryStream(bytes)),
                default);
    }

    private async Task<Guid> CriarVeiculoAsync(Guid empresaId, string placa)
    {
        await using var context = Contexto(empresaId, Guid.NewGuid());
        var cliente = new Cliente(
            empresaId,
            $"Cliente {placa}",
            TipoPessoa.PessoaFisica,
            null,
            null,
            null,
            null,
            null,
            null);
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();
        var veiculo = new Veiculo(
            empresaId,
            cliente.Id,
            placa,
            "Honda",
            "Civic",
            null,
            2024,
            2024,
            "Preto",
            1000,
            null);
        context.Veiculos.Add(veiculo);
        await context.SaveChangesAsync();
        return veiculo.Id;
    }

    private DetaraDbContext Contexto(Guid empresaId, Guid usuarioId) =>
        new(_options, new UsuarioContextoTeste(empresaId, usuarioId));

    private static byte[] ImagemJpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5];
    private static byte[] ImagemPng() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2];

    private sealed class UsuarioContextoTeste(Guid empresaId, Guid? usuarioId = null) : IUsuarioContexto
    {
        public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, Guid.Empty);
        public Guid UsuarioId { get; } = usuarioId ?? Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }

    private sealed class StorageMemoria : IArquivoStorage
    {
        private readonly Dictionary<string, byte[]> _arquivos = new(StringComparer.Ordinal);
        public int QuantidadeSalvamentos { get; private set; }
        public int QuantidadeExclusoes { get; private set; }
        public IReadOnlyCollection<string> Chaves => _arquivos.Keys;

        public async Task SalvarAsync(string chave, Stream conteudo, CancellationToken cancellationToken)
        {
            using var destino = new MemoryStream();
            await conteudo.CopyToAsync(destino, cancellationToken);
            _arquivos.Add(chave, destino.ToArray());
            QuantidadeSalvamentos++;
        }

        public Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(_arquivos.TryGetValue(chave, out var bytes)
                ? new MemoryStream(bytes, false)
                : null);

        public Task<bool> ExcluirAsync(string chave, CancellationToken cancellationToken)
        {
            QuantidadeExclusoes++;
            return Task.FromResult(_arquivos.Remove(chave));
        }

        public Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken) =>
            Task.FromResult(_arquivos.ContainsKey(chave));
    }

    private sealed class RepositorioComFalha(Veiculo veiculo) : IVeiculoFotosRepositorio
    {
        public Task<Veiculo?> ObterVeiculoAsync(Guid veiculoId, CancellationToken cancellationToken) =>
            Task.FromResult<Veiculo?>(veiculo.Id == veiculoId ? veiculo : null);
        public Task<IReadOnlyCollection<VeiculoFoto>> ListarAsync(Guid veiculoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<VeiculoFoto>>([]);
        public Task<IReadOnlyCollection<VeiculoFoto>> ListarParaAlteracaoAsync(Guid veiculoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<VeiculoFoto>>([]);
        public Task<VeiculoFoto?> ObterAsync(Guid veiculoId, Guid fotoId, bool paraAlteracao, CancellationToken cancellationToken) =>
            Task.FromResult<VeiculoFoto?>(null);
        public void Adicionar(VeiculoFoto foto) { }
        public void Remover(VeiculoFoto foto) { }
        public Task SalvarAsync(CancellationToken cancellationToken) =>
            Task.FromException(new DbUpdateException("Falha simulada."));
    }
}
