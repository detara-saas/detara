using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class IntegraFluxoOperacionalAgendaAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdensServico_EmpresaId_AgendamentoOrigemId",
                table: "OrdensServico");

            migrationBuilder.AddColumn<Guid>(
                name: "AgendamentoId",
                table: "Orcamentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Orcamentos]
                SET [AgendamentoId] = [AgendamentoOrigemId]
                WHERE [AgendamentoOrigemId] IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                WITH [VinculosOrdenados] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [EmpresaId], [AgendamentoOrigemId]
                               ORDER BY CASE WHEN [Status] IN (1, 2, 3) THEN 0 ELSE 1 END,
                                        [CriadoEmUtc] DESC,
                                        [Id]
                           ) AS [OrdemVinculo]
                    FROM [OrdensServico]
                    WHERE [AgendamentoOrigemId] IS NOT NULL
                )
                UPDATE [ordem]
                SET [AgendamentoOrigemId] = NULL
                FROM [OrdensServico] AS [ordem]
                INNER JOIN [VinculosOrdenados] AS [vinculo] ON [vinculo].[Id] = [ordem].[Id]
                WHERE [vinculo].[OrdemVinculo] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_AgendamentoOrigemId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "AgendamentoOrigemId" },
                unique: true,
                filter: "[AgendamentoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_AgendamentoId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "AgendamentoId" },
                filter: "[AgendamentoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdensServico_EmpresaId_AgendamentoOrigemId",
                table: "OrdensServico");

            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_EmpresaId_AgendamentoId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "AgendamentoId",
                table: "Orcamentos");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_AgendamentoOrigemId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "AgendamentoOrigemId" },
                filter: "[AgendamentoOrigemId] IS NOT NULL");
        }
    }
}
