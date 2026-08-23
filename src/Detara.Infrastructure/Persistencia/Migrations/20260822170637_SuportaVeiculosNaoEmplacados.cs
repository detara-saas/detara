using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class SuportaVeiculosNaoEmplacados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Veiculos_EmpresaId_Placa",
                table: "Veiculos");

            migrationBuilder.AlterColumn<string>(
                name: "Placa",
                table: "Veiculos",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7);

            migrationBuilder.AddColumn<string>(
                name: "IdentificacaoAlternativa",
                table: "Veiculos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Veiculos",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Carro");

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "OrdensServico",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "Orcamentos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "ContasReceber",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "Agendamentos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_IdentificacaoAlternativa",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "IdentificacaoAlternativa" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_Placa",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Placa" },
                unique: true,
                filter: "[Placa] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Veiculos] WHERE [Placa] IS NULL)
                   OR EXISTS (SELECT 1 FROM [Agendamentos] WHERE [VeiculoPlacaSnapshot] IS NULL)
                   OR EXISTS (SELECT 1 FROM [Orcamentos] WHERE [VeiculoPlacaSnapshot] IS NULL)
                   OR EXISTS (SELECT 1 FROM [OrdensServico] WHERE [VeiculoPlacaSnapshot] IS NULL)
                   OR EXISTS (SELECT 1 FROM [ContasReceber] WHERE [VeiculoPlacaSnapshot] IS NULL)
                    THROW 51000, 'Não é possível reverter o suporte a veículos não emplacados enquanto existirem placas nulas.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_EmpresaId_IdentificacaoAlternativa",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_EmpresaId_Placa",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "IdentificacaoAlternativa",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Veiculos");

            migrationBuilder.AlterColumn<string>(
                name: "Placa",
                table: "Veiculos",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "OrdensServico",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "Orcamentos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "ContasReceber",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoPlacaSnapshot",
                table: "Agendamentos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_Placa",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Placa" },
                unique: true);
        }
    }
}
