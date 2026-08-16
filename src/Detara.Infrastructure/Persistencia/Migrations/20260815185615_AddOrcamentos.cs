using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddOrcamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orcamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteNomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ClienteDocumentoSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClienteTelefoneSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoDescricaoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VeiculoPlacaSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AgendamentoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrcamentoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservacaoCliente = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObservacaoInterna = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Condicoes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Desconto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Acrescimo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmitidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AprovadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecusadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubstituidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AprovadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orcamentos", x => x.Id);
                    table.UniqueConstraint("AK_Orcamentos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "OrcamentosHistoricosStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrcamentosHistoricosStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrcamentosHistoricosStatus_Orcamentos_EmpresaId_OrcamentoId",
                        columns: x => new { x.EmpresaId, x.OrcamentoId },
                        principalTable: "Orcamentos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrcamentosItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoItem = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ItemCatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DescricaoSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TipoPrecificacaoReferenciaSnapshot = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    PrecoReferenciaSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrcamentosItens", x => x.Id);
                    table.UniqueConstraint("AK_OrcamentosItens_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrcamentosItens_Orcamentos_EmpresaId_OrcamentoId",
                        columns: x => new { x.EmpresaId, x.OrcamentoId },
                        principalTable: "Orcamentos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_AgendamentoOrigemId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "AgendamentoOrigemId" },
                filter: "[AgendamentoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_ClienteId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_Codigo",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true,
                filter: "[Codigo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_CriadoEmUtc",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_OrcamentoOrigemId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "OrcamentoOrigemId" },
                filter: "[OrcamentoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_Status",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_VeiculoId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentosHistoricosStatus_EmpresaId_OrcamentoId_DataUtc",
                table: "OrcamentosHistoricosStatus",
                columns: new[] { "EmpresaId", "OrcamentoId", "DataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentosItens_EmpresaId_OrcamentoId_Ordem",
                table: "OrcamentosItens",
                columns: new[] { "EmpresaId", "OrcamentoId", "Ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrcamentosHistoricosStatus");

            migrationBuilder.DropTable(
                name: "OrcamentosItens");

            migrationBuilder.DropTable(
                name: "Orcamentos");
        }
    }
}
