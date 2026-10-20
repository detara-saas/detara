using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickServiceEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtendimentoRapidoHash",
                table: "OrdensServico",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SolicitacaoAtendimentoRapidoId",
                table: "OrdensServico",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_SolicitacaoAtendimentoRapidoId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "SolicitacaoAtendimentoRapidoId" },
                unique: true,
                filter: "[SolicitacaoAtendimentoRapidoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdensServico_EmpresaId_SolicitacaoAtendimentoRapidoId",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "AtendimentoRapidoHash",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "SolicitacaoAtendimentoRapidoId",
                table: "OrdensServico");
        }
    }
}
