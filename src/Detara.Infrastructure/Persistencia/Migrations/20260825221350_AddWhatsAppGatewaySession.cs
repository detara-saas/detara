using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppGatewaySession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessoesWhatsAppEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    UltimaConexaoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoErroSeguro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesWhatsAppEmpresa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesWhatsAppEmpresa_EmpresaId",
                table: "SessoesWhatsAppEmpresa",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessoesWhatsAppEmpresa_SessionKey",
                table: "SessoesWhatsAppEmpresa",
                column: "SessionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessoesWhatsAppEmpresa");
        }
    }
}
