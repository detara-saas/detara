using Detara.Application.Abstracoes;
using Detara.Application.Veiculos;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Veiculos;

internal sealed class VeiculosRepositorio(DetaraDbContext dbContext) : IVeiculosRepositorio
{
    public async Task<PaginacaoResultado<VeiculoListaItemResultado>> ListarAsync(
        FiltroVeiculos filtro,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Veiculos.AsNoTracking();
        if (filtro.EhAtivo.HasValue)
        {
            consulta = consulta.Where(item => item.EhAtivo == filtro.EhAtivo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa))
        {
            var pesquisa = filtro.Pesquisa.Trim();
            var digitos = new string(pesquisa.Where(char.IsAsciiDigit).ToArray());
            var placa = new string(pesquisa.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            consulta = consulta.Where(item =>
                item.Placa.Contains(placa) ||
                item.Marca.Contains(pesquisa) ||
                item.Modelo.Contains(pesquisa) ||
                item.Cliente.Nome.Contains(pesquisa) ||
                digitos.Length > 0 && item.Cliente.Telefone != null && item.Cliente.Telefone.Contains(digitos));
        }

        consulta = filtro.Ordenacao == "criacao"
            ? consulta.OrderByDescending(item => item.CriadoEmUtc).ThenBy(item => item.Marca).ThenBy(item => item.Modelo)
            : consulta.OrderBy(item => item.Marca).ThenBy(item => item.Modelo).ThenBy(item => item.Placa);
        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(item => new VeiculoListaItemResultado(
                item.Id,
                item.Marca + " " + item.Modelo,
                item.Placa,
                item.ClienteId,
                item.Cliente.Nome,
                item.AnoModelo,
                item.Cor,
                item.Quilometragem,
                item.EhAtivo))
            .ToArrayAsync(cancellationToken);
        return new PaginacaoResultado<VeiculoListaItemResultado>(
            itens,
            filtro.Pagina,
            filtro.TamanhoPagina,
            total);
    }

    public Task<VeiculoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Veiculos
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new VeiculoDetalheResultado(
                item.Id,
                item.ClienteId,
                item.Cliente.Nome,
                item.Placa,
                item.Marca,
                item.Modelo,
                item.Versao,
                item.AnoFabricacao,
                item.AnoModelo,
                item.Cor,
                item.Quilometragem,
                item.Observacao,
                item.CriadoEmUtc,
                item.AtualizadoEmUtc,
                item.EhAtivo))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Veiculo?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Veiculos.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> PlacaEmUsoAsync(
        string placa,
        Guid? ignorarVeiculoId,
        CancellationToken cancellationToken) =>
        dbContext.Veiculos.AnyAsync(
            item => item.Placa == placa &&
                    (!ignorarVeiculoId.HasValue || item.Id != ignorarVeiculoId.Value),
            cancellationToken);

    public void Adicionar(Veiculo veiculo) => dbContext.Veiculos.Add(veiculo);
    public Task SalvarAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
