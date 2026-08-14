using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agendamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteNomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoDescricaoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VeiculoPlacaSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuracaoPlanejadaMinutos = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ObservacaoSolicitante = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObservacaoInterna = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agendamentos", x => x.Id);
                    table.UniqueConstraint("AK_Agendamentos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Agendamentos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgendamentosItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgendamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoItem = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ItemCatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DescricaoSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TipoPrecificacaoSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrecoReferenciaSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DuracaoReferenciaMinutosSnapshot = table.Column<int>(type: "int", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendamentosItens", x => x.Id);
                    table.UniqueConstraint("AK_AgendamentosItens_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_AgendamentosItens_Agendamentos_EmpresaId_AgendamentoId",
                        columns: x => new { x.EmpresaId, x.AgendamentoId },
                        principalTable: "Agendamentos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_EmpresaId_ClienteId",
                table: "Agendamentos",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_EmpresaId_InicioUtc",
                table: "Agendamentos",
                columns: new[] { "EmpresaId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_EmpresaId_Status_InicioUtc",
                table: "Agendamentos",
                columns: new[] { "EmpresaId", "Status", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_EmpresaId_VeiculoId",
                table: "Agendamentos",
                columns: new[] { "EmpresaId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosItens_EmpresaId_AgendamentoId_Ordem",
                table: "AgendamentosItens",
                columns: new[] { "EmpresaId", "AgendamentoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosItens_EmpresaId_AgendamentoId_TipoItem_ItemCatalogoId",
                table: "AgendamentosItens",
                columns: new[] { "EmpresaId", "AgendamentoId", "TipoItem", "ItemCatalogoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosItens_EmpresaId_TipoItem_ItemCatalogoId",
                table: "AgendamentosItens",
                columns: new[] { "EmpresaId", "TipoItem", "ItemCatalogoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendamentosItens");

            migrationBuilder.DropTable(
                name: "Agendamentos");
        }
    }
}
