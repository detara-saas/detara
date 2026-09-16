using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmacaoComercialAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmacaoComercialRegistradaEmUtc",
                table: "AssinaturasEmpresas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataConfirmacaoComercial",
                table: "AssinaturasEmpresas",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmacaoComercialRegistradaEmUtc",
                table: "AssinaturasEmpresas");

            migrationBuilder.DropColumn(
                name: "DataConfirmacaoComercial",
                table: "AssinaturasEmpresas");
        }
    }
}
