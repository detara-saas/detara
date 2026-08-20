using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddContasReceberEPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContasReceber",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoCodigoSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteNomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoDescricaoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VeiculoPlacaSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SubtotalAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DescontoAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcrescimoAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorOriginal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorRecebido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataCompetencia = table.Column<DateOnly>(type: "date", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasReceber", x => x.Id);
                    table.UniqueConstraint("AK_ContasReceber_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaReceberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormaPagamento = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Taxa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NumeroParcelas = table.Column<int>(type: "int", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecebidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistradoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EstornadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstornadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoEstorno = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.UniqueConstraint("AK_Pagamentos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Pagamentos_ContasReceber_EmpresaId_ContaReceberId",
                        columns: x => new { x.EmpresaId, x.ContaReceberId },
                        principalTable: "ContasReceber",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_ClienteId",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_CriadoEmUtc",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_DataCompetencia",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "DataCompetencia" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_DataVencimento",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_OrdemServicoId",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "OrdemServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_Status",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_EmpresaId_VeiculoId",
                table: "ContasReceber",
                columns: new[] { "EmpresaId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_EmpresaId_ContaReceberId_RecebidoEmUtc",
                table: "Pagamentos",
                columns: new[] { "EmpresaId", "ContaReceberId", "RecebidoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_EmpresaId_Status_RecebidoEmUtc",
                table: "Pagamentos",
                columns: new[] { "EmpresaId", "Status", "RecebidoEmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pagamentos");

            migrationBuilder.DropTable(
                name: "ContasReceber");
        }
    }
}
