using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddAssinaturasComerciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssinaturasEmpresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValorMensal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    InicioTeste = table.Column<DateOnly>(type: "date", nullable: false),
                    FimTeste = table.Column<DateOnly>(type: "date", nullable: false),
                    DiaVencimento = table.Column<int>(type: "int", nullable: false),
                    PrimeiroVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ProximoVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    UltimoPagamentoConfirmadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AsaasCustomerId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AsaasSubscriptionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssinaturasEmpresas", x => x.Id);
                    table.UniqueConstraint("AK_AssinaturasEmpresas_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssinaturasEmpresas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AceitesTermosAssinaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssinaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoTermo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AceitoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentoChave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HashSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ValorMensal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    PrimeiroVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    EmpresaNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmpresaDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResponsavelNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ResponsavelEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpAceite = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AceitesTermosAssinaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AceitesTermosAssinaturas_AssinaturasEmpresas_EmpresaId_AssinaturaId",
                        columns: x => new { x.EmpresaId, x.AssinaturaId },
                        principalTable: "AssinaturasEmpresas",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AceitesTermosAssinaturas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AceitesTermosAssinaturas_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricosAssinaturasEmpresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssinaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoEvento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StatusAnterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StatusNovo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OcorridoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdministradorPlataformaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenciaPagamento = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricosAssinaturasEmpresas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricosAssinaturasEmpresas_AdministradoresPlataforma_AdministradorPlataformaId",
                        column: x => x.AdministradorPlataformaId,
                        principalTable: "AdministradoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosAssinaturasEmpresas_AssinaturasEmpresas_EmpresaId_AssinaturaId",
                        columns: x => new { x.EmpresaId, x.AssinaturaId },
                        principalTable: "AssinaturasEmpresas",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosAssinaturasEmpresas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosAssinaturasEmpresas_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AceitesTermosAssinaturas_EmpresaId_AssinaturaId_VersaoTermo",
                table: "AceitesTermosAssinaturas",
                columns: new[] { "EmpresaId", "AssinaturaId", "VersaoTermo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AceitesTermosAssinaturas_EmpresaId_UsuarioId",
                table: "AceitesTermosAssinaturas",
                columns: new[] { "EmpresaId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasEmpresas_EmpresaId",
                table: "AssinaturasEmpresas",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosAssinaturasEmpresas_AdministradorPlataformaId",
                table: "HistoricosAssinaturasEmpresas",
                column: "AdministradorPlataformaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosAssinaturasEmpresas_EmpresaId_AssinaturaId_OcorridoEmUtc",
                table: "HistoricosAssinaturasEmpresas",
                columns: new[] { "EmpresaId", "AssinaturaId", "OcorridoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosAssinaturasEmpresas_EmpresaId_ReferenciaPagamento",
                table: "HistoricosAssinaturasEmpresas",
                columns: new[] { "EmpresaId", "ReferenciaPagamento" },
                unique: true,
                filter: "[ReferenciaPagamento] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosAssinaturasEmpresas_EmpresaId_UsuarioId",
                table: "HistoricosAssinaturasEmpresas",
                columns: new[] { "EmpresaId", "UsuarioId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AceitesTermosAssinaturas");

            migrationBuilder.DropTable(
                name: "HistoricosAssinaturasEmpresas");

            migrationBuilder.DropTable(
                name: "AssinaturasEmpresas");
        }
    }
}
