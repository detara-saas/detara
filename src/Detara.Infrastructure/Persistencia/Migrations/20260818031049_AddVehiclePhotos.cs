using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Veiculos_EmpresaId_Id",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateTable(
                name: "VeiculosFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveStorage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NomeOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    EhPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    CriadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeiculosFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeiculosFotos_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeiculosFotos_EmpresaId_ChaveStorage",
                table: "VeiculosFotos",
                columns: new[] { "EmpresaId", "ChaveStorage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VeiculosFotos_EmpresaId_VeiculoId_CriadoEmUtc",
                table: "VeiculosFotos",
                columns: new[] { "EmpresaId", "VeiculoId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VeiculosFotos_EmpresaId_VeiculoId_EhPrincipal",
                table: "VeiculosFotos",
                columns: new[] { "EmpresaId", "VeiculoId", "EhPrincipal" },
                unique: true,
                filter: "[EhPrincipal] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeiculosFotos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Veiculos_EmpresaId_Id",
                table: "Veiculos");
        }
    }
}
