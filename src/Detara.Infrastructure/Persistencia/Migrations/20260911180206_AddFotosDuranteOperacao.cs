using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddFotosDuranteOperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotosDuranteSnapshot",
                table: "OrdensServico",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotosDurante",
                table: "ConfiguracoesOperacionaisAtendimento",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Desabilitado");

            migrationBuilder.Sql(
                """
                UPDATE [ConfiguracoesOperacionaisAtendimento]
                SET [FotosDurante] = CASE
                    WHEN [FotosEntrada] <> 'Desabilitado' OR [FotosSaida] <> 'Desabilitado'
                        THEN 'Opcional'
                    ELSE 'Desabilitado'
                END;

                UPDATE os
                SET [ChecklistEntradaSnapshot] = COALESCE(os.[ChecklistEntradaSnapshot], cfg.[ChecklistEntrada], 'Desabilitado'),
                    [FotosEntradaSnapshot] = COALESCE(os.[FotosEntradaSnapshot], cfg.[FotosEntrada], 'Desabilitado'),
                    [FotosDuranteSnapshot] = CASE
                        WHEN os.[CheckInEmUtc] IS NOT NULL THEN 'Opcional'
                        ELSE COALESCE(cfg.[FotosDurante], 'Desabilitado')
                    END,
                    [FotosSaidaSnapshot] = COALESCE(os.[FotosSaidaSnapshot], cfg.[FotosSaida], 'Desabilitado')
                FROM [OrdensServico] AS os
                LEFT JOIN [ConfiguracoesOperacionaisAtendimento] AS cfg
                    ON cfg.[EmpresaId] = os.[EmpresaId];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotosDuranteSnapshot",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "FotosDurante",
                table: "ConfiguracoesOperacionaisAtendimento");
        }
    }
}
