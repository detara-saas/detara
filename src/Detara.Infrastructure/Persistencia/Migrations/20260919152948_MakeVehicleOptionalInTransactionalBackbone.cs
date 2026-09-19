using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MakeVehicleOptionalInTransactionalBackbone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "OrdensServico",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "OrdensServico",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "Orcamentos",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "Orcamentos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "ContasReceber",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "Agendamentos",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "Agendamentos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Agendamentos] WHERE [VeiculoId] IS NULL OR [VeiculoDescricaoSnapshot] IS NULL)
                    OR EXISTS (SELECT 1 FROM [Orcamentos] WHERE [VeiculoId] IS NULL OR [VeiculoDescricaoSnapshot] IS NULL)
                    OR EXISTS (SELECT 1 FROM [OrdensServico] WHERE [VeiculoId] IS NULL OR [VeiculoDescricaoSnapshot] IS NULL)
                    OR EXISTS (SELECT 1 FROM [ContasReceber] WHERE [VeiculoId] IS NULL OR [VeiculoDescricaoSnapshot] IS NULL)
                THROW 51000, 'Não é possível reverter a migration enquanto houver transações sem veículo.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "OrdensServico",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "OrdensServico",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "Orcamentos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "Orcamentos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "ContasReceber",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VeiculoId",
                table: "Agendamentos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VeiculoDescricaoSnapshot",
                table: "Agendamentos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
