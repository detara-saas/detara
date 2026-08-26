using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class FinalizaExperienciaWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroConectado",
                table: "SessoesWhatsAppEmpresa",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAtivacaoWhatsAppEmUtc",
                table: "ConfiguracoesNotificacaoEmpresa",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirComunicacaoWhatsApp",
                table: "ConfiguracoesNotificacaoEmpresa",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioAtivacaoWhatsAppId",
                table: "ConfiguracoesNotificacaoEmpresa",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrdemServicoId",
                table: "ComunicacoesCliente",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "ComunicacoesCliente",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroConectado",
                table: "SessoesWhatsAppEmpresa");

            migrationBuilder.DropColumn(
                name: "DataAtivacaoWhatsAppEmUtc",
                table: "ConfiguracoesNotificacaoEmpresa");

            migrationBuilder.DropColumn(
                name: "PermitirComunicacaoWhatsApp",
                table: "ConfiguracoesNotificacaoEmpresa");

            migrationBuilder.DropColumn(
                name: "UsuarioAtivacaoWhatsAppId",
                table: "ConfiguracoesNotificacaoEmpresa");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrdemServicoId",
                table: "ComunicacoesCliente",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "ComunicacoesCliente",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
