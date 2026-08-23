using System.Globalization;
using System.Text;
using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;

namespace Detara.Infrastructure.Atendimento;

internal sealed class PdfOrcamentoGenerator : IOrcamentoPdfGenerator
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public byte[] Gerar(DocumentoPdfOrcamento documento)
    {
        var canvas = new PdfCanvas(documento.Empresa.NomeFantasia, documento.Orcamento.Orcamento.Codigo ?? "ORÇAMENTO");
        var o = documento.Orcamento.Orcamento;
        var identificacaoEmpresa = string.Join("  •  ", new[]
        {
            $"CPF/CNPJ: {documento.Empresa.CpfCnpj}",
            string.IsNullOrWhiteSpace(documento.Empresa.Telefone) ? null : $"Telefone: {documento.Empresa.Telefone}",
            documento.Empresa.Email
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
        canvas.Titulo("ORÇAMENTO", o.Codigo ?? string.Empty, identificacaoEmpresa);
        canvas.DuasColunas("EMISSÃO", o.EmitidoEmUtc?.ToString("dd/MM/yyyy", Cultura) ?? "—", "VALIDADE", o.ValidoAte.ToString("dd/MM/yyyy", Cultura));
        canvas.Secao("CLIENTE");
        canvas.Texto(o.ClienteNome, 12, negrito: true);
        canvas.Texto(string.Join("  •  ", new[] { FormatarDocumento(o.ClienteDocumento), FormatarTelefone(o.ClienteTelefone) }.Where(x => x is not null)));
        canvas.Secao("VEÍCULO");
        canvas.Texto(string.IsNullOrWhiteSpace(o.VeiculoPlaca) ||
                     o.VeiculoDescricao.Contains(o.VeiculoPlaca, StringComparison.OrdinalIgnoreCase)
            ? o.VeiculoDescricao
            : $"{o.VeiculoDescricao}  •  {o.VeiculoPlaca}", 11, negrito: true);
        canvas.Secao("ITENS");
        canvas.CabecalhoItens();
        foreach (var item in o.Itens.OrderBy(x => x.Ordem))
            canvas.Item(item.Nome, item.Quantidade, item.ValorUnitario, item.Quantidade * item.ValorUnitario, item.Observacao);
        canvas.Resumo(o.Itens.Sum(x => x.Quantidade * x.ValorUnitario), o.Desconto, o.Acrescimo,
            o.Itens.Sum(x => x.Quantidade * x.ValorUnitario) - o.Desconto + o.Acrescimo);
        if (!string.IsNullOrWhiteSpace(o.ObservacaoCliente)) { canvas.Secao("OBSERVAÇÕES"); canvas.Paragrafo(o.ObservacaoCliente!); }
        if (!string.IsNullOrWhiteSpace(o.Condicoes)) { canvas.Secao("CONDIÇÕES"); canvas.Paragrafo(o.Condicoes!); }
        canvas.RodapeFinal($"Proposta válida até {o.ValidoAte:dd/MM/yyyy}. Aprovação registrada pela empresa não representa assinatura digital.");
        return canvas.Gerar();
    }

    private static string Dinheiro(decimal valor) => valor.ToString("C2", Cultura).Replace('\u00A0', ' ');
    private static string? FormatarDocumento(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : $"CPF/CNPJ: {valor}";
    private static string? FormatarTelefone(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : $"Telefone: {valor}";

    private sealed class PdfCanvas
    {
        private readonly string _empresa;
        private readonly string _codigo;
        private readonly List<StringBuilder> _paginas = [];
        private StringBuilder _conteudo = null!;
        private decimal _y;

        public PdfCanvas(string empresa, string codigo) { _empresa = empresa; _codigo = codigo; NovaPagina(); }

        public void Titulo(string titulo, string codigo, string identificacaoEmpresa)
        {
            Texto(titulo, 22, true, 48);
            Texto(codigo, 12, false, 48);
            Texto(identificacaoEmpresa, 8.5m, false, 48);
            _y -= 8;
            LinhaHorizontal();
        }

        public void DuasColunas(string rotuloA, string valorA, string rotuloB, string valorB)
        {
            Garantir(44);
            Escrever(rotuloA, 8, true, 48, _y); Escrever(valorA, 11, false, 48, _y - 15);
            Escrever(rotuloB, 8, true, 330, _y); Escrever(valorB, 11, false, 330, _y - 15);
            _y -= 42;
        }

        public void Secao(string titulo)
        {
            Garantir(34);
            _y -= 8;
            Cor(0, 0.55m, 0.42m);
            Escrever(titulo, 9, true, 48, _y);
            Cor(0.08m, 0.1m, 0.14m);
            _y -= 18;
        }

        public void Texto(string texto, decimal tamanho = 10, bool negrito = false, decimal x = 48)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            Garantir(tamanho + 8);
            Escrever(texto, tamanho, negrito, x, _y);
            _y -= tamanho + 7;
        }

        public void Paragrafo(string texto)
        {
            foreach (var linha in Quebrar(texto, 92)) Texto(linha, 10);
            _y -= 4;
        }

        public void CabecalhoItens()
        {
            Garantir(30);
            Cor(0.94m, 0.96m, 0.97m); RetanguloPreenchido(48, _y - 17, 499, 22); Cor(0.25m, 0.3m, 0.36m);
            Escrever("DESCRIÇÃO", 8, true, 56, _y - 4); Escrever("QTD.", 8, true, 356, _y - 4);
            Escrever("UNITÁRIO", 8, true, 405, _y - 4); Escrever("SUBTOTAL", 8, true, 486, _y - 4);
            Cor(0.08m, 0.1m, 0.14m); _y -= 29;
        }

        public void Item(string nome, int quantidade, decimal unitario, decimal subtotal, string? observacao)
        {
            var linhas = Quebrar(nome, 48).ToArray();
            var altura = Math.Max(26, linhas.Length * 13 + (string.IsNullOrWhiteSpace(observacao) ? 0 : 13));
            Garantir(altura + 6, repetirCabecalhoItens: true);
            for (var i = 0; i < linhas.Length; i++) Escrever(linhas[i], 9.5m, i == 0, 56, _y - i * 13);
            Escrever(quantidade.ToString(Cultura), 9.5m, false, 370, _y);
            Escrever(Dinheiro(unitario), 9.5m, false, 405, _y);
            Escrever(Dinheiro(subtotal), 9.5m, true, 486, _y);
            if (!string.IsNullOrWhiteSpace(observacao)) Escrever(Quebrar(observacao!, 50).First(), 8, false, 56, _y - linhas.Length * 13);
            _y -= altura;
            Cor(0.82m, 0.85m, 0.88m); Linha(48, _y + 5, 547, _y + 5); Cor(0.08m, 0.1m, 0.14m);
        }

        public void Resumo(decimal subtotal, decimal desconto, decimal acrescimo, decimal total)
        {
            Garantir(105);
            _y -= 6;
            LinhaResumo("Subtotal", subtotal, false);
            if (desconto > 0) LinhaResumo("Desconto", -desconto, false);
            if (acrescimo > 0) LinhaResumo("Acréscimo", acrescimo, false);
            Cor(0, 0.55m, 0.42m); Linha(350, _y + 7, 547, _y + 7); Cor(0.08m, 0.1m, 0.14m);
            Escrever("TOTAL", 11, true, 350, _y - 8); Escrever(Dinheiro(total), 16, true, 468, _y - 8); _y -= 38;
        }

        public void RodapeFinal(string texto)
        {
            Garantir(48);
            _y -= 8;
            foreach (var linha in Quebrar(texto, 100)) Texto(linha, 8);
        }

        public byte[] Gerar() => PdfWriter.Gerar(_paginas.Select(x => Encoding.Latin1.GetBytes(x.ToString())).ToArray());

        private void LinhaResumo(string rotulo, decimal valor, bool negrito)
        { Escrever(rotulo, 9.5m, negrito, 350, _y); Escrever(Dinheiro(valor), 9.5m, negrito, 486, _y); _y -= 19; }
        private void Garantir(decimal altura, bool repetirCabecalhoItens = false) { if (_y - altura >= 90) return; NovaPagina(); if (repetirCabecalhoItens) CabecalhoItens(); }
        private void NovaPagina()
        {
            _conteudo = new StringBuilder(); _paginas.Add(_conteudo); _y = 790;
            Cor(0, 0.55m, 0.42m); RetanguloPreenchido(0, 818, 595, 24); Cor(0.08m, 0.1m, 0.14m);
            Escrever(_empresa, 14, true, 48, 795); Escrever(_codigo, 8, false, 460, 795);
            Cor(0.45m, 0.49m, 0.55m); Escrever($"Página {_paginas.Count}", 8, false, 500, 35); Escrever("Gerado por Detara", 8, false, 48, 35); Cor(0.08m, 0.1m, 0.14m);
            _y = 758;
        }
        private void LinhaHorizontal() { Cor(0, 0.55m, 0.42m); Linha(48, _y, 547, _y); Cor(0.08m, 0.1m, 0.14m); _y -= 24; }
        private void Escrever(string texto, decimal tamanho, bool negrito, decimal x, decimal y) => _conteudo.Append("BT /").Append(negrito ? "F2" : "F1").Append(' ').Append(N(tamanho)).Append(" Tf 1 0 0 1 ").Append(N(x)).Append(' ').Append(N(y)).Append(" Tm (").Append(Escapar(texto)).Append(") Tj ET\n");
        private void Linha(decimal x1, decimal y1, decimal x2, decimal y2) => _conteudo.Append(N(x1)).Append(' ').Append(N(y1)).Append(" m ").Append(N(x2)).Append(' ').Append(N(y2)).Append(" l S\n");
        private void RetanguloPreenchido(decimal x, decimal y, decimal largura, decimal altura) => _conteudo.Append(N(x)).Append(' ').Append(N(y)).Append(' ').Append(N(largura)).Append(' ').Append(N(altura)).Append(" re f\n");
        private void Cor(decimal r, decimal g, decimal b) => _conteudo.Append(N(r)).Append(' ').Append(N(g)).Append(' ').Append(N(b)).Append(" rg ").Append(N(r)).Append(' ').Append(N(g)).Append(' ').Append(N(b)).Append(" RG\n");
        private static string N(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", " ").Replace("\n", " ");
        private static IEnumerable<string> Quebrar(string texto, int limite)
        {
            var palavras = texto.Replace("\r", "").Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var linha = new StringBuilder();
            foreach (var palavra in palavras)
            {
                if (linha.Length > 0 && linha.Length + 1 + palavra.Length > limite) { yield return linha.ToString(); linha.Clear(); }
                if (linha.Length > 0) linha.Append(' '); linha.Append(palavra);
            }
            if (linha.Length > 0) yield return linha.ToString();
        }
    }

    private static class PdfWriter
    {
        public static byte[] Gerar(IReadOnlyCollection<byte[]> conteudos)
        {
            var paginas = conteudos.ToArray();
            var objetos = new List<byte[]> { Array.Empty<byte>() };
            objetos.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
            var kids = string.Join(' ', Enumerable.Range(0, paginas.Length).Select(i => $"{5 + i * 2} 0 R"));
            objetos.Add(Ascii($"<< /Type /Pages /Count {paginas.Length} /Kids [{kids}] >>"));
            objetos.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objetos.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
            for (var i = 0; i < paginas.Length; i++)
            {
                var paginaNumero = 5 + i * 2; var conteudoNumero = paginaNumero + 1;
                objetos.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {conteudoNumero} 0 R >>"));
                objetos.Add(Concatenar(Ascii($"<< /Length {paginas[i].Length} >>\nstream\n"), paginas[i], Ascii("\nendstream")));
            }
            using var stream = new MemoryStream();
            stream.Write(Ascii("%PDF-1.7\n%")); stream.Write([0xE2, 0xE3, 0xCF, 0xD3]); stream.Write(Ascii("\n"));
            var offsets = new List<long> { 0 };
            for (var i = 1; i < objetos.Count; i++)
            {
                offsets.Add(stream.Position); stream.Write(Ascii($"{i} 0 obj\n")); stream.Write(objetos[i]); stream.Write(Ascii("\nendobj\n"));
            }
            var xref = stream.Position;
            stream.Write(Ascii($"xref\n0 {objetos.Count}\n0000000000 65535 f \n"));
            foreach (var offset in offsets.Skip(1)) stream.Write(Ascii($"{offset:D10} 00000 n \n"));
            stream.Write(Ascii($"trailer\n<< /Size {objetos.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
            return stream.ToArray();
        }

        private static byte[] Ascii(string texto) => Encoding.ASCII.GetBytes(texto);
        private static byte[] Concatenar(params byte[][] partes) { var tamanho = partes.Sum(x => x.Length); var resultado = new byte[tamanho]; var posicao = 0; foreach (var parte in partes) { Buffer.BlockCopy(parte, 0, resultado, posicao, parte.Length); posicao += parte.Length; } return resultado; }
    }
}
