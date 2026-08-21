using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdministracaoBasicaTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Perfis_EmpresaId_Nome",
                table: "Perfis");

            migrationBuilder.DropIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_UsuarioId",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.AddColumn<long>(
                name: "Versao",
                table: "Usuarios",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "VersaoSeguranca",
                table: "Usuarios",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Perfis",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EhSistema",
                table: "Perfis",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NomeNormalizado",
                table: "Perfis",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Versao",
                table: "Perfis",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "VersaoCadastro",
                table: "Empresas",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AlterColumn<Guid>(
                name: "CriadoPorAdministradorPlataformaId",
                table: "ConvitesAdministradoresEmpresa",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origem",
                table: "ConvitesAdministradoresEmpresa",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                "UPDATE [Perfis] SET [NomeNormalizado] = UPPER(LTRIM(RTRIM([Nome]))), " +
                "[EhSistema] = CASE WHEN UPPER(LTRIM(RTRIM([Nome]))) = N'ADMINISTRADOR' " +
                "THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;");

            migrationBuilder.CreateIndex(
                name: "IX_Perfis_EmpresaId_NomeNormalizado",
                table: "Perfis",
                columns: new[] { "EmpresaId", "NomeNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "EmpresaId", "CriadoPorUsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_UsuarioId_Origem",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "EmpresaId", "UsuarioId", "Origem" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConvitesAdministradoresEmpresa_Usuarios_EmpresaId_CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "EmpresaId", "CriadoPorUsuarioId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [ConvitesAdministradoresEmpresa] WHERE [Origem] = 2;");

            migrationBuilder.DropForeignKey(
                name: "FK_ConvitesAdministradoresEmpresa_Usuarios_EmpresaId_CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.DropIndex(
                name: "IX_Perfis_EmpresaId_NomeNormalizado",
                table: "Perfis");

            migrationBuilder.DropIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.DropIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_UsuarioId_Origem",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VersaoSeguranca",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "Perfis");

            migrationBuilder.DropColumn(
                name: "EhSistema",
                table: "Perfis");

            migrationBuilder.DropColumn(
                name: "NomeNormalizado",
                table: "Perfis");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Perfis");

            migrationBuilder.DropColumn(
                name: "VersaoCadastro",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CriadoPorUsuarioId",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "ConvitesAdministradoresEmpresa");

            migrationBuilder.AlterColumn<Guid>(
                name: "CriadoPorAdministradorPlataformaId",
                table: "ConvitesAdministradoresEmpresa",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Perfis_EmpresaId_Nome",
                table: "Perfis",
                columns: new[] { "EmpresaId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_UsuarioId",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "EmpresaId", "UsuarioId" });
        }
    }
}
