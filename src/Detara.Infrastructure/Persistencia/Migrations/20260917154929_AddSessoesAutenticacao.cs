using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddSessoesAutenticacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessoesAutenticacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoIdentidade = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdministradorPlataformaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersaoSegurancaIdentidade = table.Column<long>(type: "bigint", nullable: false),
                    VersaoSegurancaEmpresa = table.Column<long>(type: "bigint", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(44)", maxLength: 44, nullable: false),
                    Persistente = table.Column<bool>(type: "bit", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoUsoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevogadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubstituidoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoRevogacao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesAutenticacao", x => x.Id);
                    table.CheckConstraint("CK_SessoesAutenticacao_Identidade", "([TipoIdentidade] = 'Tenant' AND [UsuarioId] IS NOT NULL AND [EmpresaId] IS NOT NULL AND [AdministradorPlataformaId] IS NULL AND [VersaoSegurancaEmpresa] IS NOT NULL) OR ([TipoIdentidade] = 'AdministradorPlataforma' AND [UsuarioId] IS NULL AND [EmpresaId] IS NULL AND [AdministradorPlataformaId] IS NOT NULL AND [VersaoSegurancaEmpresa] IS NULL)");
                    table.ForeignKey(
                        name: "FK_SessoesAutenticacao_AdministradoresPlataforma_AdministradorPlataformaId",
                        column: x => x.AdministradorPlataformaId,
                        principalTable: "AdministradoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessoesAutenticacao_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_AdministradorPlataformaId",
                table: "SessoesAutenticacao",
                column: "AdministradorPlataformaId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_EmpresaId_UsuarioId",
                table: "SessoesAutenticacao",
                columns: new[] { "EmpresaId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_ExpiraEmUtc",
                table: "SessoesAutenticacao",
                column: "ExpiraEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_FamiliaId_RevogadoEmUtc",
                table: "SessoesAutenticacao",
                columns: new[] { "FamiliaId", "RevogadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_TokenHash",
                table: "SessoesAutenticacao",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAutenticacao_UsuarioId_EmpresaId",
                table: "SessoesAutenticacao",
                columns: new[] { "UsuarioId", "EmpresaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessoesAutenticacao");
        }
    }
}
