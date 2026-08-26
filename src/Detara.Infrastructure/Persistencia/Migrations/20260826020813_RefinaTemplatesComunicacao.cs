using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RefinaTemplatesComunicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplatesEmailEmpresa_EmpresaId_Tipo",
                table: "TemplatesEmailEmpresa");

            migrationBuilder.RenameTable(
                name: "TemplatesEmailEmpresa",
                newName: "TemplatesComunicacaoEmpresa");

            migrationBuilder.RenameColumn(
                name: "CorpoHtmlSanitizado",
                table: "TemplatesComunicacaoEmpresa",
                newName: "Conteudo");

            migrationBuilder.AlterColumn<string>(
                name: "Assunto",
                table: "TemplatesComunicacaoEmpresa",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Canal",
                table: "TemplatesComunicacaoEmpresa",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Email");

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "TemplatesComunicacaoEmpresa",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "Veículo pronto para retirada");

            migrationBuilder.AddColumn<string>(
                name: "TemplateNomeSnapshot",
                table: "ComunicacoesCliente",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesComunicacaoEmpresa_EmpresaId_Canal_Tipo",
                table: "TemplatesComunicacaoEmpresa",
                columns: new[] { "EmpresaId", "Canal", "Tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplatesComunicacaoEmpresa_EmpresaId_Canal_Tipo",
                table: "TemplatesComunicacaoEmpresa");

            migrationBuilder.Sql(
                "DELETE FROM [TemplatesComunicacaoEmpresa] WHERE [Canal] <> N'Email';");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "TemplatesComunicacaoEmpresa");

            migrationBuilder.DropColumn(
                name: "Nome",
                table: "TemplatesComunicacaoEmpresa");

            migrationBuilder.AlterColumn<string>(
                name: "Assunto",
                table: "TemplatesComunicacaoEmpresa",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "Conteudo",
                table: "TemplatesComunicacaoEmpresa",
                newName: "CorpoHtmlSanitizado");

            migrationBuilder.RenameTable(
                name: "TemplatesComunicacaoEmpresa",
                newName: "TemplatesEmailEmpresa");

            migrationBuilder.DropColumn(
                name: "TemplateNomeSnapshot",
                table: "ComunicacoesCliente");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesEmailEmpresa_EmpresaId_Tipo",
                table: "TemplatesEmailEmpresa",
                columns: new[] { "EmpresaId", "Tipo" },
                unique: true);
        }
    }
}
