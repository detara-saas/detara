using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogPricingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoPrecificacao",
                table: "Servicos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SobConsulta");

            migrationBuilder.AddColumn<string>(
                name: "TipoPrecificacao",
                table: "Pacotes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SobConsulta");

            migrationBuilder.Sql("UPDATE [Servicos] SET [TipoPrecificacao] = CASE WHEN [PrecoBase] IS NULL THEN 'SobConsulta' ELSE 'Fixo' END;");
            migrationBuilder.Sql("UPDATE [Pacotes] SET [TipoPrecificacao] = CASE WHEN [Preco] IS NULL THEN 'SobConsulta' ELSE 'Fixo' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoPrecificacao",
                table: "Servicos");

            migrationBuilder.DropColumn(
                name: "TipoPrecificacao",
                table: "Pacotes");
        }
    }
}
