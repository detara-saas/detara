using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VersaoSeguranca",
                table: "Empresas",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "AdministradoresPlataforma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmailNormalizado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MfaHabilitado = table.Column<bool>(type: "bit", nullable: false),
                    SegredoTotpProtegido = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimoTimestepTotpAceito = table.Column<long>(type: "bigint", nullable: true),
                    VersaoSeguranca = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    UltimoLoginEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministradoresPlataforma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriasPlataforma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorPlataformaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoAcao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmpresaAlvoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntidadeAlvoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    DescricaoSegura = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasPlataforma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriasPlataforma_AdministradoresPlataforma_AdministradorPlataformaId",
                        column: x => x.AdministradorPlataformaId,
                        principalTable: "AdministradoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditoriasPlataforma_Empresas_EmpresaAlvoId",
                        column: x => x.EmpresaAlvoId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodigosRecuperacaoAdministradoresPlataforma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorPlataformaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UtilizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosRecuperacaoAdministradoresPlataforma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigosRecuperacaoAdministradoresPlataforma_AdministradoresPlataforma_AdministradorPlataformaId",
                        column: x => x.AdministradorPlataformaId,
                        principalTable: "AdministradoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConvitesAdministradoresEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailDestinoSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessamentoIniciadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnviadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AceitoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvalidadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoPorAdministradorPlataformaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantidadeTentativasEnvio = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEnvioEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoErroSeguro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvitesAdministradoresEmpresa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvitesAdministradoresEmpresa_AdministradoresPlataforma_CriadoPorAdministradorPlataformaId",
                        column: x => x.CriadoPorAdministradorPlataformaId,
                        principalTable: "AdministradoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConvitesAdministradoresEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConvitesAdministradoresEmpresa_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministradoresPlataforma_EmailNormalizado",
                table: "AdministradoresPlataforma",
                column: "EmailNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasPlataforma_AdministradorPlataformaId_CriadoEmUtc",
                table: "AuditoriasPlataforma",
                columns: new[] { "AdministradorPlataformaId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasPlataforma_CriadoEmUtc",
                table: "AuditoriasPlataforma",
                column: "CriadoEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasPlataforma_EmpresaAlvoId_CriadoEmUtc",
                table: "AuditoriasPlataforma",
                columns: new[] { "EmpresaAlvoId", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosRecuperacaoAdministradoresPlataforma_AdministradorPlataformaId_CodigoHash",
                table: "CodigosRecuperacaoAdministradoresPlataforma",
                columns: new[] { "AdministradorPlataformaId", "CodigoHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_CriadoPorAdministradorPlataformaId",
                table: "ConvitesAdministradoresEmpresa",
                column: "CriadoPorAdministradorPlataformaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_EmpresaId_UsuarioId",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "EmpresaId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_Status_ProximaTentativaEnvioEmUtc",
                table: "ConvitesAdministradoresEmpresa",
                columns: new[] { "Status", "ProximaTentativaEnvioEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAdministradoresEmpresa_TokenHash",
                table: "ConvitesAdministradoresEmpresa",
                column: "TokenHash",
                unique: true,
                filter: "[TokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasPlataforma");

            migrationBuilder.DropTable(
                name: "CodigosRecuperacaoAdministradoresPlataforma");

            migrationBuilder.DropTable(
                name: "ConvitesAdministradoresEmpresa");

            migrationBuilder.DropTable(
                name: "AdministradoresPlataforma");

            migrationBuilder.DropColumn(
                name: "VersaoSeguranca",
                table: "Empresas");
        }
    }
}
