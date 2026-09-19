using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SegmentoCodigo",
                table: "Empresas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "estetica-automotiva");

            migrationBuilder.CreateTable(
                name: "EmpresasCapacidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Habilitada = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresasCapacidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmpresasCapacidades_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmpresasCapacidades_EmpresaId_Codigo",
                table: "EmpresasCapacidades",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [EmpresasCapacidades]
                    ([Id], [EmpresaId], [Codigo], [Habilitada], [Versao], [CriadoEmUtc], [AtualizadoEmUtc], [EhAtivo])
                SELECT NEWID(), e.[Id], c.[Codigo], CAST(1 AS bit), 1, SYSUTCDATETIME(), NULL, CAST(1 AS bit)
                FROM [Empresas] e
                CROSS JOIN (VALUES
                    (N'clientes'),
                    (N'servicos'),
                    (N'pacotes'),
                    (N'agenda'),
                    (N'orcamentos'),
                    (N'ordem-servico'),
                    (N'financeiro'),
                    (N'despesas'),
                    (N'veiculos'),
                    (N'check-in')
                ) c([Codigo])
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [EmpresasCapacidades] existente
                    WHERE existente.[EmpresaId] = e.[Id]
                      AND existente.[Codigo] = c.[Codigo]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmpresasCapacidades");

            migrationBuilder.DropColumn(
                name: "SegmentoCodigo",
                table: "Empresas");
        }
    }
}
