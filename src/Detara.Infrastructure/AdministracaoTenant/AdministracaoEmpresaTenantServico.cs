using Detara.Application.Abstracoes;
using Detara.Application.AdministracaoTenant;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.AdministracaoTenant;

internal sealed class AdministracaoEmpresaTenantServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto) : IAdministracaoEmpresaTenantServico
{
    public async Task<EmpresaTenantResultado> ObterAsync(CancellationToken cancellationToken)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == usuarioContexto.EmpresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        return Mapear(empresa);
    }

    public async Task<EmpresaTenantResultado> AtualizarAsync(
        string nomeFantasia,
        string razaoSocial,
        string cpfCnpj,
        string? email,
        string? telefone,
        string fusoHorario,
        long versao,
        CancellationToken cancellationToken)
    {
        var documento = SomenteDigitos(cpfCnpj);
        if (documento.Length is not (11 or 14))
        {
            throw new ArgumentException("O CPF/CNPJ deve possuir 11 ou 14 dígitos.", nameof(cpfCnpj));
        }

        if (!TimeZoneValido(fusoHorario))
        {
            throw new ArgumentException("O fuso horário informado é inválido.", nameof(fusoHorario));
        }

        var empresa = await db.Empresas
            .SingleOrDefaultAsync(x => x.Id == usuarioContexto.EmpresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        if (await db.Empresas.AsNoTracking().AnyAsync(
                x => x.Id != empresa.Id && x.CpfCnpj == documento,
                cancellationToken))
        {
            throw new ConflitoRegraNegocioException("Já existe uma empresa com este documento.");
        }

        try
        {
            empresa.AtualizarCadastro(nomeFantasia, razaoSocial, documento, email, telefone,
                fusoHorario, versao);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflitoRegraNegocioException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Mapear(empresa);
    }

    private static EmpresaTenantResultado Mapear(Empresa empresa) => new(
        empresa.NomeFantasia,
        empresa.RazaoSocial,
        empresa.CpfCnpj,
        empresa.Email,
        empresa.Telefone,
        empresa.Slug,
        empresa.FusoHorario,
        empresa.EhAtivo,
        empresa.CriadoEmUtc,
        empresa.VersaoCadastro);

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());

    private static bool TimeZoneValido(string valor)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(valor);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
