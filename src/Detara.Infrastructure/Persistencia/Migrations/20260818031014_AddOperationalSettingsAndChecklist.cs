using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalSettingsAndChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistModelos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistModelos", x => x.Id);
                    table.UniqueConstraint("AK_ChecklistModelos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesOperacionaisAtendimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistEntrada = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FotosEntrada = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FotosSaida = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesOperacionaisAtendimento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistModeloItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistModeloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistModeloItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistModeloItens_ChecklistModelos_EmpresaId_ChecklistModeloId",
                        columns: x => new { x.EmpresaId, x.ChecklistModeloId },
                        principalTable: "ChecklistModelos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModeloItens_EmpresaId_ChecklistModeloId_Ordem",
                table: "ChecklistModeloItens",
                columns: new[] { "EmpresaId", "ChecklistModeloId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModelos_EmpresaId",
                table: "ChecklistModelos",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesOperacionaisAtendimento_EmpresaId",
                table: "ConfiguracoesOperacionaisAtendimento",
                column: "EmpresaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistModeloItens");

            migrationBuilder.DropTable(
                name: "ConfiguracoesOperacionaisAtendimento");

            migrationBuilder.DropTable(
                name: "ChecklistModelos");
        }
    }
}
