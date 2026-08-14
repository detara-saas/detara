namespace Detara.Domain.Entidades;

public static class DocumentoFiscal
{
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var documento = new string(valor.Where(char.IsAsciiDigit).ToArray());
        return documento.Length == 0 ? null : documento;
    }

    public static bool EhValido(string documento, TipoPessoa tipoPessoa) =>
        tipoPessoa == TipoPessoa.PessoaFisica
            ? ValidarCpf(documento)
            : ValidarCnpj(documento);

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Length != 11 || TodosDigitosIguais(cpf))
        {
            return false;
        }

        return CalcularDigito(cpf, 9, 10) == cpf[9] - '0' &&
               CalcularDigito(cpf, 10, 11) == cpf[10] - '0';
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Length != 14 || TodosDigitosIguais(cnpj))
        {
            return false;
        }

        var pesosPrimeiro = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var pesosSegundo = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        return CalcularDigitoCnpj(cnpj, pesosPrimeiro) == cnpj[12] - '0' &&
               CalcularDigitoCnpj(cnpj, pesosSegundo) == cnpj[13] - '0';
    }

    private static int CalcularDigito(string valor, int quantidade, int pesoInicial)
    {
        var soma = 0;
        for (var indice = 0; indice < quantidade; indice++)
        {
            soma += (valor[indice] - '0') * (pesoInicial - indice);
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int CalcularDigitoCnpj(string valor, IReadOnlyList<int> pesos)
    {
        var soma = 0;
        for (var indice = 0; indice < pesos.Count; indice++)
        {
            soma += (valor[indice] - '0') * pesos[indice];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static bool TodosDigitosIguais(string valor) => valor.All(item => item == valor[0]);
}
