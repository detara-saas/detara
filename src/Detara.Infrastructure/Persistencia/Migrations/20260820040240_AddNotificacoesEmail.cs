using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificacoesEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesNotificacaoEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnviarVeiculoProntoAutomaticamente = table.Column<bool>(type: "bit", nullable: false),
                    ResponderParaEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AtualizadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesNotificacaoEmpresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificacoesEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DestinatarioEmailSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinatarioNomeSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AssuntoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorpoHtmlSnapshot = table.Column<string>(type: "nvarchar(max)", maxLength: 102400, nullable: false),
                    OrigemTemplate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResponderParaSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    QuantidadeTentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessamentoIniciadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnviadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProvedorMensagemId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UltimoErroSeguro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoProximaTentativa = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProximaTentativaSolicitadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacoesEmail", x => x.Id);
                    table.UniqueConstraint("AK_NotificacoesEmail_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "TemplatesEmailEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Assunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorpoHtmlSanitizado = table.Column<string>(type: "nvarchar(max)", maxLength: 51200, nullable: false),
                    CriadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtualizadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesEmailEmpresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TentativasNotificacaoEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificacaoEmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SolicitadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ProvedorMensagemId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErroSeguro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TentativasNotificacaoEmail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TentativasNotificacaoEmail_NotificacoesEmail_EmpresaId_NotificacaoEmailId",
                        columns: x => new { x.EmpresaId, x.NotificacaoEmailId },
                        principalTable: "NotificacoesEmail",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesNotificacaoEmpresa_EmpresaId",
                table: "ConfiguracoesNotificacaoEmpresa",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Status_ProximaTentativaEmUtc",
                table: "NotificacoesEmail",
                columns: new[] { "EmpresaId", "Status", "ProximaTentativaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Tipo_OrdemServicoId",
                table: "NotificacoesEmail",
                columns: new[] { "EmpresaId", "Tipo", "OrdemServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesEmailEmpresa_EmpresaId_Tipo",
                table: "TemplatesEmailEmpresa",
                columns: new[] { "EmpresaId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TentativasNotificacaoEmail_EmpresaId_NotificacaoEmailId_Numero",
                table: "TentativasNotificacaoEmail",
                columns: new[] { "EmpresaId", "NotificacaoEmailId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesNotificacaoEmpresa");

            migrationBuilder.DropTable(
                name: "TemplatesEmailEmpresa");

            migrationBuilder.DropTable(
                name: "TentativasNotificacaoEmail");

            migrationBuilder.DropTable(
                name: "NotificacoesEmail");
        }
    }
}
