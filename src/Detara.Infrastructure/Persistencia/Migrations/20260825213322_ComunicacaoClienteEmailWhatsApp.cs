using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ComunicacaoClienteEmailWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanalAutomaticoVeiculoPronto",
                table: "ConfiguracoesNotificacaoEmpresa",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Nenhum");

            migrationBuilder.Sql("""
                UPDATE [ConfiguracoesNotificacaoEmpresa]
                SET [CanalAutomaticoVeiculoPronto] =
                    CASE WHEN [EnviarVeiculoProntoAutomaticamente] = 1 THEN 'Email' ELSE 'Nenhum' END;
                """);

            migrationBuilder.DropColumn(
                name: "EnviarVeiculoProntoAutomaticamente",
                table: "ConfiguracoesNotificacaoEmpresa");

            migrationBuilder.CreateTable(
                name: "ComunicacoesCliente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mensagem = table.Column<string>(type: "nvarchar(max)", maxLength: 102400, nullable: false),
                    DestinatarioSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SolicitadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataEnvioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessamentoIniciadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProvedorMensagemId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UltimoErroSeguro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EhAtivo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComunicacoesCliente", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComunicacoesCliente_EmpresaId_Canal_Status_ProcessamentoIniciadoEmUtc",
                table: "ComunicacoesCliente",
                columns: new[] { "EmpresaId", "Canal", "Status", "ProcessamentoIniciadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ComunicacoesCliente_EmpresaId_OrdemServicoId_CriadoEmUtc",
                table: "ComunicacoesCliente",
                columns: new[] { "EmpresaId", "OrdemServicoId", "CriadoEmUtc" });

            migrationBuilder.Sql("""
                INSERT INTO [ComunicacoesCliente]
                    ([Id], [ClienteId], [OrdemServicoId], [Canal], [Tipo], [Mensagem],
                     [DestinatarioSnapshot], [Status], [Origem], [SolicitadoPorUsuarioId],
                     [DataEnvioUtc], [ProcessamentoIniciadoEmUtc], [ProvedorMensagemId],
                     [UltimoErroSeguro], [Versao], [CriadoEmUtc], [AtualizadoEmUtc],
                     [EhAtivo], [EmpresaId])
                SELECT
                    n.[Id], n.[ClienteId], n.[OrdemServicoId], 'Email', 'VeiculoPronto',
                    n.[CorpoHtmlSnapshot], n.[DestinatarioEmailSnapshot],
                    CASE
                        WHEN n.[Status] = 'Enviada' THEN 'Enviado'
                        WHEN n.[Status] IN ('Falhou', 'SemDestinatario') THEN 'Falhou'
                        ELSE 'Pendente'
                    END,
                    CASE
                        WHEN n.[TipoProximaTentativa] = 'Manual' OR
                             n.[ProximaTentativaSolicitadaPorUsuarioId] IS NOT NULL OR
                             EXISTS (SELECT 1 FROM [TentativasNotificacaoEmail] t
                                     WHERE t.[EmpresaId] = n.[EmpresaId]
                                       AND t.[NotificacaoEmailId] = n.[Id]
                                       AND t.[Tipo] = 'Manual')
                        THEN 'Manual' ELSE 'Automatica'
                    END,
                    COALESCE(n.[ProximaTentativaSolicitadaPorUsuarioId],
                        (SELECT TOP (1) t.[SolicitadoPorUsuarioId]
                         FROM [TentativasNotificacaoEmail] t
                         WHERE t.[EmpresaId] = n.[EmpresaId]
                           AND t.[NotificacaoEmailId] = n.[Id]
                           AND t.[SolicitadoPorUsuarioId] IS NOT NULL
                         ORDER BY t.[Numero] DESC)),
                    n.[EnviadaEmUtc], n.[ProcessamentoIniciadoEmUtc], n.[ProvedorMensagemId],
                    CASE WHEN n.[Status] = 'SemDestinatario'
                         THEN COALESCE(n.[UltimoErroSeguro], 'O cliente não possui um e-mail válido cadastrado.')
                         ELSE n.[UltimoErroSeguro] END,
                    n.[Versao], n.[CriadoEmUtc], n.[AtualizadoEmUtc], n.[EhAtivo], n.[EmpresaId]
                FROM [NotificacoesEmail] n;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComunicacoesCliente");

            migrationBuilder.AddColumn<bool>(
                name: "EnviarVeiculoProntoAutomaticamente",
                table: "ConfiguracoesNotificacaoEmpresa",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE [ConfiguracoesNotificacaoEmpresa]
                SET [EnviarVeiculoProntoAutomaticamente] =
                    CASE WHEN [CanalAutomaticoVeiculoPronto] = 'Email' THEN 1 ELSE 0 END;
                """);

            migrationBuilder.DropColumn(
                name: "CanalAutomaticoVeiculoPronto",
                table: "ConfiguracoesNotificacaoEmpresa");
        }
    }
}
