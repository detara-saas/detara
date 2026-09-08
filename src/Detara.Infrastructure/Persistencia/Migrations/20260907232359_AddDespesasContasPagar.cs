using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddDespesasContasPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasDespesa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomeNormalizado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasDespesa", x => x.Id);
                    table.UniqueConstraint("AK_CategoriasDespesa_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "DespesasRecorrentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoriaDespesaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiaVencimento = table.Column<int>(type: "int", nullable: false),
                    CompetenciaInicial = table.Column<DateOnly>(type: "date", nullable: false),
                    CompetenciaFinal = table.Column<DateOnly>(type: "date", nullable: true),
                    ProximaCompetencia = table.Column<DateOnly>(type: "date", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DespesasRecorrentes", x => x.Id);
                    table.UniqueConstraint("AK_DespesasRecorrentes_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_DespesasRecorrentes_CategoriasDespesa_EmpresaId_CategoriaDespesaId",
                        columns: x => new { x.EmpresaId, x.CategoriaDespesaId },
                        principalTable: "CategoriasDespesa",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContasPagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoriaDespesaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoriaNomeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DespesaRecorrenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    ValorPago = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Fornecedor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CanceladoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanceladoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasPagar", x => x.Id);
                    table.UniqueConstraint("AK_ContasPagar_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContasPagar_CategoriasDespesa_EmpresaId_CategoriaDespesaId",
                        columns: x => new { x.EmpresaId, x.CategoriaDespesaId },
                        principalTable: "CategoriasDespesa",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContasPagar_DespesasRecorrentes_EmpresaId_DespesaRecorrenteId",
                        columns: x => new { x.EmpresaId, x.DespesaRecorrenteId },
                        principalTable: "DespesasRecorrentes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagamentosContasPagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RegistradoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EstornadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstornadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoEstorno = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosContasPagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosContasPagar_ContasPagar_EmpresaId_ContaPagarId",
                        columns: x => new { x.EmpresaId, x.ContaPagarId },
                        principalTable: "ContasPagar",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasDespesa_EmpresaId_NomeNormalizado",
                table: "CategoriasDespesa",
                columns: new[] { "EmpresaId", "NomeNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EmpresaId_CategoriaDespesaId_Competencia",
                table: "ContasPagar",
                columns: new[] { "EmpresaId", "CategoriaDespesaId", "Competencia" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EmpresaId_Competencia_Status_DataVencimento",
                table: "ContasPagar",
                columns: new[] { "EmpresaId", "Competencia", "Status", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EmpresaId_DespesaRecorrenteId_Competencia",
                table: "ContasPagar",
                columns: new[] { "EmpresaId", "DespesaRecorrenteId", "Competencia" },
                unique: true,
                filter: "[DespesaRecorrenteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DespesasRecorrentes_EhAtivo_ProximaCompetencia_EmpresaId",
                table: "DespesasRecorrentes",
                columns: new[] { "EhAtivo", "ProximaCompetencia", "EmpresaId" });

            migrationBuilder.CreateIndex(
                name: "IX_DespesasRecorrentes_EmpresaId_CategoriaDespesaId",
                table: "DespesasRecorrentes",
                columns: new[] { "EmpresaId", "CategoriaDespesaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosContasPagar_EmpresaId_ContaPagarId",
                table: "PagamentosContasPagar",
                columns: new[] { "EmpresaId", "ContaPagarId" },
                unique: true,
                filter: "[Status] = 'Confirmado'");

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosContasPagar_EmpresaId_DataPagamento_Status",
                table: "PagamentosContasPagar",
                columns: new[] { "EmpresaId", "DataPagamento", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagamentosContasPagar");

            migrationBuilder.DropTable(
                name: "ContasPagar");

            migrationBuilder.DropTable(
                name: "DespesasRecorrentes");

            migrationBuilder.DropTable(
                name: "CategoriasDespesa");
        }
    }
}
