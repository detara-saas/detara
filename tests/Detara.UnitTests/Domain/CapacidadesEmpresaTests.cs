using Detara.Domain.Capacidades;

namespace Detara.UnitTests.Domain;

public sealed class CapacidadesEmpresaTests
{
    [Fact]
    public void Catalogo_NaoPossuiCodigosDuplicados()
    {
        Assert.Equal(
            CatalogoCapacidadesEmpresa.Todas.Count,
            CatalogoCapacidadesEmpresa.Todas.Select(item => item.Codigo).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PresetAutomotivo_HabilitaTodasAsCapacidadesDeterministicamente()
    {
        var empresaId = Guid.NewGuid();
        var primeiro = PresetCapacidadesEmpresa.Criar(empresaId, SegmentosEmpresa.EsteticaAutomotiva);
        var segundo = PresetCapacidadesEmpresa.Criar(empresaId, SegmentosEmpresa.EsteticaAutomotiva);

        Assert.Equal(CatalogoCapacidadesEmpresa.Todas.Select(item => item.Codigo), primeiro.Select(item => item.Codigo));
        Assert.Equal(primeiro.Select(item => item.Codigo), segundo.Select(item => item.Codigo));
        Assert.All(primeiro, item => Assert.True(item.Habilitada));
    }

    [Fact]
    public void CheckInHabilitadoSemVeiculos_EhRejeitado()
    {
        var estado = CatalogoCapacidadesEmpresa.Todas.ToDictionary(item => item.Codigo, _ => true);
        estado[CodigosCapacidadeEmpresa.Veiculos] = false;

        var exception = Assert.Throws<ArgumentException>(() =>
            CatalogoCapacidadesEmpresa.ValidarConfiguracao(estado));

        Assert.Contains("Check-in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CapacidadeDesconhecida_EhRejeitadaComSeguranca()
    {
        var estado = new Dictionary<string, bool> { ["desconhecida"] = true };
        Assert.Throws<ArgumentException>(() => CatalogoCapacidadesEmpresa.ValidarConfiguracao(estado));
        Assert.False(CatalogoCapacidadesEmpresa.TentarObter("desconhecida", out _));
    }
}
