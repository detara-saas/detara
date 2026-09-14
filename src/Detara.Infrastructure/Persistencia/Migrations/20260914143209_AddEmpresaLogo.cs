using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoArquivoChave",
                table: "Empresas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LogoAtualizadaEmUtc",
                table: "Empresas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoTokenPublico",
                table: "Empresas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LogoVersao",
                table: "Empresas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_LogoTokenPublico",
                table: "Empresas",
                column: "LogoTokenPublico",
                unique: true,
                filter: "[LogoTokenPublico] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Empresas_LogoTokenPublico",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoArquivoChave",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoAtualizadaEmUtc",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoTokenPublico",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoVersao",
                table: "Empresas");
        }
    }
}
