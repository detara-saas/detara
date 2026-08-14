using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInterfacePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Usuarios_EmpresaId_Id",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateTable(
                name: "UsuariosPreferencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tema = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Idioma = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SidebarRecolhida = table.Column<bool>(type: "bit", nullable: false),
                    PaginaInicial = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosPreferencias", x => x.Id);
                    table.UniqueConstraint("AK_UsuariosPreferencias_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_UsuariosPreferencias_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosPaginasFavoritas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioPreferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pagina = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosPaginasFavoritas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosPaginasFavoritas_UsuariosPreferencias_EmpresaId_UsuarioPreferenciaId",
                        columns: x => new { x.EmpresaId, x.UsuarioPreferenciaId },
                        principalTable: "UsuariosPreferencias",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosPaginasFavoritas_EmpresaId_UsuarioPreferenciaId_Ordem",
                table: "UsuariosPaginasFavoritas",
                columns: new[] { "EmpresaId", "UsuarioPreferenciaId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosPaginasFavoritas_EmpresaId_UsuarioPreferenciaId_Pagina",
                table: "UsuariosPaginasFavoritas",
                columns: new[] { "EmpresaId", "UsuarioPreferenciaId", "Pagina" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosPreferencias_EmpresaId_UsuarioId",
                table: "UsuariosPreferencias",
                columns: new[] { "EmpresaId", "UsuarioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsuariosPaginasFavoritas");

            migrationBuilder.DropTable(
                name: "UsuariosPreferencias");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Usuarios_EmpresaId_Id",
                table: "Usuarios");
        }
    }
}
