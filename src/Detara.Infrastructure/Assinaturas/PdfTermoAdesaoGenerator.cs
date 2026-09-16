using System.Globalization;
using Detara.Application.Assinaturas;
using SkiaSharp;

namespace Detara.Infrastructure.Assinaturas;

internal sealed class PdfTermoAdesaoGenerator : IGeradorPdfTermoAssinatura
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] Clausulas =
    [
        "1. Objeto e aceite",
        "1.1. O presente Termo disciplina a contratação e o uso do Detara, plataforma de software destinada à gestão de operações de estética automotiva, incluindo funcionalidades disponibilizadas conforme a versão vigente do produto.",
        "1.2. A contratação poderá ocorrer por aceite eletrônico, confirmação comercial ou outro meio que permita demonstrar a concordância do CONTRATANTE com estas condições e com o quadro-resumo aplicável à contratação.",
        "1.3. O aceite eletrônico poderá ser registrado com informações como empresa, usuário responsável, data e hora, versão do termo e demais evidências técnicas adequadas à comprovação do aceite.",
        "2. Período de teste gratuito",
        "2.1. O CONTRATANTE poderá utilizar o Detara gratuitamente por 7 (sete) dias, contados a partir da ativação do ambiente de teste ou de outra data expressamente informada no início da avaliação.",
        "2.2. O período de teste não gera obrigação de permanência. Caso o CONTRATANTE opte por não prosseguir com a assinatura, poderá solicitar o encerramento do acesso ao final do teste.",
        "2.3. Por opção comercial do Detara, poderá existir período adicional de utilização sem cobrança entre o término dos 7 (sete) dias de teste e o primeiro vencimento padrão, sem que isso altere a duração oficial do período de teste.",
        "3. Mensalidade, vencimento e cobrança",
        "3.1. O valor mensal será aquele apresentado e aceito no momento da contratação. Na condição comercial atual, a mensalidade de referência é de R$ 120,00 (cento e vinte reais), podendo existir condições comerciais específicas registradas para determinado CONTRATANTE.",
        "3.2. O vencimento padrão das mensalidades ocorrerá no dia 10 de cada mês.",
        "3.3. A primeira mensalidade terá vencimento no primeiro dia 10 que ocorrer após o encerramento integral do período de teste gratuito. Se o teste encerrar no próprio dia 10, o primeiro vencimento poderá ser fixado no dia 10 do mês seguinte.",
        "3.4. As cobranças poderão ser emitidas por plataforma de pagamentos de terceiros, atualmente o Asaas, ficando o CONTRATANTE sujeito também aos meios, prazos de compensação e procedimentos técnicos disponibilizados pelo respectivo provedor.",
        "3.5. O pagamento de uma mensalidade corresponde à continuidade do acesso ao Detara no ciclo de assinatura subsequente, observadas as regras de vencimento, atraso, suspensão e cancelamento previstas neste Termo.",
        "4. Atraso, carência operacional e suspensão",
        "4.1. O não pagamento até a data de vencimento caracterizará atraso da mensalidade.",
        "4.2. Enquanto a cobrança possuir menos de 7 (sete) dias corridos de atraso, o Detara poderá manter o acesso operacional ativo como tolerância comercial, sem que isso represente renúncia ao valor devido.",
        "4.3. Ao completar 7 (sete) dias corridos de atraso, o acesso às funcionalidades operacionais do Detara poderá ser temporariamente suspenso até a regularização. Em uma cobrança com vencimento no dia 10, por exemplo, a suspensão poderá ocorrer a partir do dia 17 caso o pagamento ainda não tenha sido identificado.",
        "4.4. A suspensão por inadimplência não implica exclusão imediata dos dados do CONTRATANTE nem cancelamento automático da conta.",
        "4.5. Durante a suspensão, o Detara poderá limitar a navegação a uma tela informativa de pendência, canais de suporte e orientações para regularização, ainda que o pagamento em si seja realizado fora da aplicação, diretamente por meio do provedor de cobrança utilizado.",
        "5. Confirmação do pagamento e reativação",
        "5.1. A reativação ocorrerá após a identificação e confirmação do pagamento pelo Detara, seja por informação recebida do provedor de cobrança, seja por validação manual enquanto não houver integração automatizada disponível.",
        "5.2. O envio de comprovante poderá auxiliar a conferência, mas a liberação poderá depender da efetiva confirmação da transação ou de validação administrativa.",
        "5.3. Após a confirmação, o Detara buscará restabelecer o acesso em prazo operacional razoável, sem necessidade de nova contratação, desde que a assinatura não tenha sido cancelada.",
        "6. Cancelamento e encerramento",
        "6.1. Salvo condição comercial específica previamente aceita, a assinatura não possui fidelidade nem multa de permanência.",
        "6.2. O cancelamento poderá ser solicitado por meio do canal oficial de atendimento do Detara. Cobranças já vencidas e valores já devidos permanecem exigíveis até sua regularização.",
        "6.3. O cancelamento da assinatura é distinto da suspensão temporária por atraso. O tratamento, retenção e eventual exclusão dos dados após o encerramento observarão a Política de Privacidade do Detara e as obrigações legais aplicáveis.",
        "7. Responsabilidades do CONTRATANTE",
        "7.1. O CONTRATANTE é responsável pela veracidade das informações cadastradas, pela gestão dos usuários vinculados à sua empresa e pela proteção de suas credenciais de acesso.",
        "7.2. O CONTRATANTE compromete-se a utilizar o Detara de forma lícita, compatível com sua finalidade e sem tentar comprometer a segurança, a disponibilidade ou a integridade da plataforma ou de terceiros.",
        "7.3. O CONTRATANTE é responsável pelos dados, imagens, documentos, mensagens e demais conteúdos que inserir ou utilizar na plataforma, inclusive quanto à existência de base legal ou autorização quando necessárias.",
        "8. Disponibilidade, manutenção e suporte",
        "8.1. O Detara buscará manter o serviço disponível e seguro, podendo realizar manutenções, atualizações e intervenções técnicas necessárias ao funcionamento, à segurança ou à evolução do produto.",
        "8.2. Interrupções decorrentes de serviços de terceiros, infraestrutura de internet, provedores externos, manutenção emergencial ou eventos fora do controle razoável do Detara poderão afetar temporariamente a disponibilidade.",
        "8.3. O suporte será prestado pelo canal oficial detara.saas@gmail.com, sem prejuízo de outros canais que venham a ser formalmente divulgados pelo Detara, nos horários e condições operacionais vigentes.",
        "9. Dados, privacidade e segurança",
        "9.1. O tratamento de dados pessoais relacionado ao uso do Detara observará a legislação brasileira aplicável e a Política de Privacidade disponibilizada pela plataforma.",
        "9.2. Cada empresa CONTRATANTE permanece responsável pelas obrigações que lhe sejam aplicáveis em relação aos dados pessoais de seus próprios clientes, colaboradores e demais titulares cujas informações sejam inseridas no Detara.",
        "9.3. A suspensão de acesso por atraso não autoriza, por si só, a exclusão imediata dos dados armazenados no ambiente do CONTRATANTE.",
        "10. Propriedade intelectual",
        "10.1. O software, identidade visual, marca, código, interfaces, documentação e demais elementos próprios do Detara permanecem de titularidade de seus respectivos proprietários e licenciadores.",
        "10.2. A assinatura concede ao CONTRATANTE apenas o direito de uso da plataforma durante a vigência da contratação, não havendo transferência de propriedade intelectual.",
        "11. Alterações comerciais e do termo",
        "11.1. O Detara poderá atualizar este Termo para refletir mudanças legais, técnicas ou comerciais, comunicando alterações relevantes ao CONTRATANTE por meio adequado.",
        "11.2. Eventuais reajustes de preço serão informados previamente e não afetarão valores já pagos, observadas as condições comerciais apresentadas ao CONTRATANTE e a legislação aplicável.",
        "12. Comunicações",
        "12.1. Avisos de cobrança, vencimento, atraso, suspensão, manutenção, segurança e outras comunicações relacionadas à assinatura poderão ser enviados por e-mail, WhatsApp, notificações na plataforma ou pelos canais do provedor de pagamento utilizado.",
        "12.2. O CONTRATANTE deve manter seus dados de contato atualizados para receber comunicações relacionadas ao serviço.",
        "13. Disposições gerais",
        "13.1. A eventual tolerância do Detara quanto ao descumprimento de alguma obrigação não representará renúncia ao direito de exigir seu cumprimento posteriormente.",
        "13.2. Se alguma disposição deste Termo for considerada inválida ou inexequível, as demais permanecerão em vigor na medida permitida pela legislação aplicável.",
        "13.3. Este Termo será regido pela legislação brasileira. Questões não resolvidas pelos canais de atendimento poderão ser submetidas ao foro legalmente competente, respeitadas as regras obrigatórias de competência aplicáveis ao caso."
    ];

    public byte[] Gerar(DadosTermoAssinatura dados)
    {
        using var stream = new MemoryStream();
        using var documento = SKDocument.CreatePdf(stream);
        using var texto = new SKPaint { Color = new SKColor(24, 31, 42), IsAntialias = true };
        using var destaque = new SKPaint { Color = new SKColor(0, 139, 107), IsAntialias = true };
        using var fonteRegular = new SKFont(SKTypeface.Default, 10);
        using var fonteTitulo = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 18);
        using var fonteSubtitulo = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 11);
        var pagina = new Pagina(documento, texto, destaque, fonteRegular, fonteTitulo, fonteSubtitulo);
        pagina.Titulo("DETARA", "Termo de Adesão e Condições de Assinatura", "Software de gestão para estética automotiva");
        pagina.Caixa("MINUTA PARA REVISÃO. Este documento organiza as regras comerciais e operacionais definidas para a primeira fase do Detara. Antes de disponibilizá-lo para aceite de clientes, recomenda-se revisão por profissional jurídico, especialmente antes do uso definitivo com clientes.");
        pagina.Secao("Quadro-resumo da contratação");
        pagina.LinhaResumo("Produto", "Detara — plataforma de gestão para estética automotiva");
        pagina.LinhaResumo("CONTRATADA", "Gustavo Steilein Navroski — pessoa física responsável pelo Detara");
        pagina.LinhaResumo("CPF", "139.752.179-10");
        pagina.LinhaResumo("CONTRATANTE", $"{dados.EmpresaNome} — CPF/CNPJ {dados.EmpresaDocumento}");
        pagina.LinhaResumo("Responsável", $"{dados.ResponsavelNome} — {dados.ResponsavelEmail}");
        pagina.LinhaResumo("Mensalidade", $"{dados.ValorMensal.ToString("C2", Cultura)} na condição comercial aceita");
        pagina.LinhaResumo("Período de teste", $"7 (sete) dias gratuitos — {dados.DataInicio:dd/MM/yyyy} a {dados.FimTeste:dd/MM/yyyy}");
        pagina.LinhaResumo("Vencimento padrão", $"Dia {dados.DiaVencimento} de cada mês");
        pagina.LinhaResumo("Primeiro vencimento", dados.PrimeiroVencimento.ToString("dd/MM/yyyy", Cultura));
        pagina.LinhaResumo("Cobrança", "Assinatura recorrente gerada por provedor de pagamentos terceiro, atualmente Asaas");
        pagina.LinhaResumo("Suspensão por atraso", "A partir do momento em que a cobrança completar 7 (sete) dias corridos de atraso");
        pagina.LinhaResumo("Cancelamento", "Sem fidelidade ou multa de permanência, salvo condição comercial específica previamente aceita");
        pagina.LinhaResumo("Suporte", "detara.saas@gmail.com");
        foreach (var clausula in Clausulas)
        {
            if (char.IsDigit(clausula[0]) && clausula.Contains(". ") && !char.IsDigit(clausula[Math.Min(2, clausula.Length - 1)])) pagina.Secao(clausula);
            else pagina.Paragrafo(clausula);
        }
        pagina.Secao("Registro de aceite");
        pagina.Paragrafo("Ao aceitar eletronicamente este Termo, o responsável declara possuir poderes ou autorização para vincular a empresa CONTRATANTE às condições acima.");
        pagina.LinhaResumo("Empresa CONTRATANTE", dados.EmpresaNome);
        pagina.LinhaResumo("Responsável pelo aceite", dados.ResponsavelNome);
        pagina.LinhaResumo("E-mail", dados.ResponsavelEmail);
        pagina.LinhaResumo("Data do aceite", dados.AceitoEmUtc?.ToString("dd/MM/yyyy HH:mm 'UTC'", Cultura) ?? "Pendente de aceite eletrônico");
        pagina.LinhaResumo("Versão do termo", $"v{dados.VersaoTermo}");
        pagina.Finalizar();
        documento.Close();
        return stream.ToArray();
    }

    private sealed class Pagina(SKDocument documento, SKPaint texto, SKPaint destaque,
        SKFont fonteRegular, SKFont fonteTitulo, SKFont fonteSubtitulo)
    {
        private SKCanvas _canvas = null!;
        private float _y;
        private int _numero;
        private const float Esquerda = 48;
        private const float Direita = 547;

        public void Titulo(string marca, string nome, string descricao)
        {
            NovaPagina();
            _canvas.DrawText(marca, Esquerda, _y, SKTextAlign.Left, fonteTitulo, destaque); _y += 29;
            _canvas.DrawText(nome, Esquerda, _y, SKTextAlign.Left, fonteSubtitulo, texto); _y += 19;
            _canvas.DrawText(descricao, Esquerda, _y, SKTextAlign.Left, fonteRegular, texto); _y += 25;
        }

        public void Secao(string valor)
        {
            Garantir(38);
            _y += 10;
            _canvas.DrawText(valor, Esquerda, _y, SKTextAlign.Left, fonteSubtitulo, texto);
            _y += 18;
        }

        public void Paragrafo(string valor)
        {
            foreach (var linha in Quebrar(valor, 103))
            {
                Garantir(14);
                _canvas.DrawText(linha, Esquerda, _y, SKTextAlign.Left, fonteRegular, texto);
                _y += 14;
            }
            _y += 5;
        }

        public void Caixa(string valor)
        {
            var linhas = Quebrar(valor, 92).ToArray();
            Garantir(linhas.Length * 14 + 26);
            using var fundo = new SKPaint { Color = new SKColor(245, 247, 250) };
            _canvas.DrawRoundRect(Esquerda, _y - 12, Direita - Esquerda, linhas.Length * 14 + 20, 5, 5, fundo);
            foreach (var linha in linhas) { _canvas.DrawText(linha, Esquerda + 10, _y + 4, SKTextAlign.Left, fonteRegular, texto); _y += 14; }
            _y += 18;
        }

        public void LinhaResumo(string rotulo, string valor)
        {
            var linhas = Quebrar(valor, 72).ToArray();
            Garantir(Math.Max(24, linhas.Length * 14 + 8));
            _canvas.DrawText(rotulo, Esquerda, _y, SKTextAlign.Left, fonteSubtitulo, texto);
            for (var i = 0; i < linhas.Length; i++) _canvas.DrawText(linhas[i], 175, _y + i * 14, SKTextAlign.Left, fonteRegular, texto);
            _y += Math.Max(24, linhas.Length * 14 + 8);
        }

        public void Finalizar() { if (_canvas is not null) documento.EndPage(); }

        private void Garantir(float altura)
        {
            if (_y + altura < 795) return;
            documento.EndPage();
            NovaPagina();
        }

        private void NovaPagina()
        {
            _numero++;
            _canvas = documento.BeginPage(595, 842);
            _y = 52;
            using var rodape = new SKPaint { Color = new SKColor(110, 120, 135), IsAntialias = true };
            using var fonteRodape = new SKFont(SKTypeface.Default, 8);
            _canvas.DrawText($"Detara · Termo v0.3 · Página {_numero}", Esquerda, 818,
                SKTextAlign.Left, fonteRodape, rodape);
        }

        private static IEnumerable<string> Quebrar(string valor, int limite)
        {
            var palavras = valor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var linha = new System.Text.StringBuilder();
            foreach (var palavra in palavras)
            {
                if (linha.Length > 0 && linha.Length + palavra.Length + 1 > limite)
                {
                    yield return linha.ToString();
                    linha.Clear();
                }
                if (linha.Length > 0) linha.Append(' ');
                linha.Append(palavra);
            }
            if (linha.Length > 0) yield return linha.ToString();
        }
    }
}
