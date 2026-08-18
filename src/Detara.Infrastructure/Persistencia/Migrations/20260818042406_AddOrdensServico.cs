using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdensServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrdemServicoOrigemId",
                table: "Orcamentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrdensServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    OrcamentoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgendamentoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteNomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ClienteDocumentoSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClienteTelefoneSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoDescricaoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VeiculoPlacaSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DuracaoPlanejadaMinutos = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DescontoAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcrescimoAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AutorizacaoDiretaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutorizacaoDiretaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObservacaoAutorizacaoDireta = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CheckInEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckInPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuilometragemEntrada = table.Column<int>(type: "int", nullable: true),
                    ObservacaoEntrada = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChecklistEntradaSnapshot = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FotosEntradaSnapshot = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FotosSaidaSnapshot = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IniciadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecucaoFinalizadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecucaoFinalizadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanceladaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensServico", x => x.Id);
                    table.UniqueConstraint("AK_OrdensServico_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "OrdensServicoChecklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensServicoChecklist", x => x.Id);
                    table.UniqueConstraint("AK_OrdensServicoChecklist_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrdensServicoChecklist_OrdensServico_EmpresaId_OrdemServicoId",
                        columns: x => new { x.EmpresaId, x.OrdemServicoId },
                        principalTable: "OrdensServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdensServicoFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChaveStorage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NomeOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    EnviadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensServicoFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdensServicoFotos_OrdensServico_EmpresaId_OrdemServicoId",
                        columns: x => new { x.EmpresaId, x.OrdemServicoId },
                        principalTable: "OrdensServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdensServicoHistoricosStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_OrdensServicoHistoricosStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdensServicoHistoricosStatus_OrdensServico_EmpresaId_OrdemServicoId",
                        columns: x => new { x.EmpresaId, x.OrdemServicoId },
                        principalTable: "OrdensServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdensServicoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoItem = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ItemCatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrcamentoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrcamentoItemOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DescricaoSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValorUnitarioAutorizado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    OrigemComercial = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AutorizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AutorizadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObservacaoAutorizacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensServicoItens", x => x.Id);
                    table.UniqueConstraint("AK_OrdensServicoItens_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrdensServicoItens_OrdensServico_EmpresaId_OrdemServicoId",
                        columns: x => new { x.EmpresaId, x.OrdemServicoId },
                        principalTable: "OrdensServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdensServicoChecklistItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DescricaoSnapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Resposta = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensServicoChecklistItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdensServicoChecklistItens_OrdensServicoChecklist_EmpresaId_ChecklistId",
                        columns: x => new { x.EmpresaId, x.ChecklistId },
                        principalTable: "OrdensServicoChecklist",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_EmpresaId_OrdemServicoOrigemId",
                table: "Orcamentos",
                columns: new[] { "EmpresaId", "OrdemServicoOrigemId" },
                filter: "[OrdemServicoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_AgendamentoOrigemId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "AgendamentoOrigemId" },
                filter: "[AgendamentoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_ClienteId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_Codigo",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_CriadoEmUtc",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_OrcamentoOrigemId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "OrcamentoOrigemId" },
                unique: true,
                filter: "[OrcamentoOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_Status",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServico_EmpresaId_VeiculoId",
                table: "OrdensServico",
                columns: new[] { "EmpresaId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoChecklist_EmpresaId_OrdemServicoId",
                table: "OrdensServicoChecklist",
                columns: new[] { "EmpresaId", "OrdemServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoChecklistItens_EmpresaId_ChecklistId_Ordem",
                table: "OrdensServicoChecklistItens",
                columns: new[] { "EmpresaId", "ChecklistId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoFotos_ChaveStorage",
                table: "OrdensServicoFotos",
                column: "ChaveStorage",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoFotos_EmpresaId_OrdemServicoId_Categoria",
                table: "OrdensServicoFotos",
                columns: new[] { "EmpresaId", "OrdemServicoId", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoHistoricosStatus_EmpresaId_OrdemServicoId_DataUtc",
                table: "OrdensServicoHistoricosStatus",
                columns: new[] { "EmpresaId", "OrdemServicoId", "DataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoItens_EmpresaId_OrcamentoItemOrigemId",
                table: "OrdensServicoItens",
                columns: new[] { "EmpresaId", "OrcamentoItemOrigemId" },
                unique: true,
                filter: "[OrcamentoItemOrigemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensServicoItens_EmpresaId_OrdemServicoId_Ordem",
                table: "OrdensServicoItens",
                columns: new[] { "EmpresaId", "OrdemServicoId", "Ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdensServicoChecklistItens");

            migrationBuilder.DropTable(
                name: "OrdensServicoFotos");

            migrationBuilder.DropTable(
                name: "OrdensServicoHistoricosStatus");

            migrationBuilder.DropTable(
                name: "OrdensServicoItens");

            migrationBuilder.DropTable(
                name: "OrdensServicoChecklist");

            migrationBuilder.DropTable(
                name: "OrdensServico");

            migrationBuilder.DropIndex(
                name: "IX_Orcamentos_EmpresaId_OrdemServicoOrigemId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "OrdemServicoOrigemId",
                table: "Orcamentos");
        }
    }
}
