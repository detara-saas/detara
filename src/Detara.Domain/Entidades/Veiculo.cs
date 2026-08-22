using System.Text.RegularExpressions;

namespace Detara.Domain.Entidades;

public sealed class Veiculo : EntidadeEmpresaBase
{
    private Veiculo()
    {
    }

    public Veiculo(
        Guid empresaId,
        Guid clienteId,
        TipoVeiculo tipo,
        string? placa,
        string? identificacaoAlternativa,
        string marca,
        string modelo,
        string? versao,
        int? anoFabricacao,
        int? anoModelo,
        string? cor,
        int? quilometragem,
        string? observacao)
        : base(Guid.NewGuid(), empresaId)
    {
        Atualizar(clienteId, tipo, placa, identificacaoAlternativa, marca, modelo, versao,
            anoFabricacao, anoModelo, cor, quilometragem, observacao);
    }

    public Veiculo(Guid empresaId, Guid clienteId, string placa, string marca, string modelo,
        string? versao, int? anoFabricacao, int? anoModelo, string? cor, int? quilometragem,
        string? observacao)
        : this(empresaId, clienteId, TipoVeiculo.Carro, placa, null, marca, modelo, versao,
            anoFabricacao, anoModelo, cor, quilometragem, observacao)
    {
    }

    public Guid ClienteId { get; private set; }
    public TipoVeiculo Tipo { get; private set; }
    public string? Placa { get; private set; }
    public string? IdentificacaoAlternativa { get; private set; }
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public string? Versao { get; private set; }
    public int? AnoFabricacao { get; private set; }
    public int? AnoModelo { get; private set; }
    public string? Cor { get; private set; }
    public int? Quilometragem { get; private set; }
    public string? Observacao { get; private set; }
    public Cliente Cliente { get; private set; } = null!;

    public void Atualizar(
        Guid clienteId,
        TipoVeiculo tipo,
        string? placa,
        string? identificacaoAlternativa,
        string marca,
        string modelo,
        string? versao,
        int? anoFabricacao,
        int? anoModelo,
        string? cor,
        int? quilometragem,
        string? observacao)
    {
        ClienteId = clienteId == Guid.Empty
            ? throw new ArgumentException("O cliente deve ser informado.", nameof(clienteId))
            : clienteId;
        Tipo = Enum.IsDefined(tipo)
            ? tipo
            : throw new ArgumentException("O tipo de veículo é inválido.", nameof(tipo));
        Placa = NormalizarPlaca(placa);
        IdentificacaoAlternativa = NormalizarOpcional(identificacaoAlternativa, 120);
        Marca = Exigir(marca, 80, nameof(marca));
        Modelo = Exigir(modelo, 80, nameof(modelo));
        Versao = NormalizarOpcional(versao, 80);
        ValidarAno(anoFabricacao, nameof(anoFabricacao));
        ValidarAno(anoModelo, nameof(anoModelo));
        AnoFabricacao = anoFabricacao;
        AnoModelo = anoModelo;
        Cor = NormalizarOpcional(cor, 50);
        Quilometragem = quilometragem is null or >= 0
            ? quilometragem
            : throw new ArgumentException("A quilometragem não pode ser negativa.", nameof(quilometragem));
        Observacao = NormalizarOpcional(observacao, 2000);
        MarcarComoAtualizada();
    }

    public void Atualizar(Guid clienteId, string placa, string marca, string modelo,
        string? versao, int? anoFabricacao, int? anoModelo, string? cor, int? quilometragem,
        string? observacao) =>
        Atualizar(clienteId, Tipo == 0 ? TipoVeiculo.Carro : Tipo, placa,
            IdentificacaoAlternativa, marca, modelo, versao, anoFabricacao, anoModelo, cor,
            quilometragem, observacao);

    public static string? NormalizarPlaca(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var placa = new string((valor ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (!Regex.IsMatch(placa, "^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A placa deve seguir o padrão ABC1234 ou ABC1D23.", nameof(valor));
        }

        return placa;
    }

    public static string FormatarDescricao(
        string marca,
        string modelo,
        string? placa,
        string? identificacaoAlternativa)
    {
        var veiculo = string.Join(" ", new[] { marca?.Trim(), modelo?.Trim() }
            .Where(item => !string.IsNullOrWhiteSpace(item)));
        var identificacao = !string.IsNullOrWhiteSpace(placa)
            ? placa.Trim()
            : string.IsNullOrWhiteSpace(identificacaoAlternativa)
                ? null
                : identificacaoAlternativa.Trim();
        return identificacao is null ? veiculo : $"{veiculo} · {identificacao}";
    }

    private static void ValidarAno(int? ano, string parametro)
    {
        if (ano is not null && (ano < 1886 || ano > DateTime.UtcNow.Year + 2))
        {
            throw new ArgumentException("O ano do veículo está fora da faixa permitida.", parametro);
        }
    }

    private static string Exigir(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
