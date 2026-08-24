using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Detara.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PermiteReenvioNotificacaoVeiculoPronto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Tipo_OrdemServicoId",
                table: "NotificacoesEmail");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Tipo_OrdemServicoId",
                table: "NotificacoesEmail",
                columns: new[] { "EmpresaId", "Tipo", "OrdemServicoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [NotificacoesEmail]
                    GROUP BY [EmpresaId], [Tipo], [OrdemServicoId]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Não é possível restaurar a unicidade enquanto existirem reenvios históricos.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Tipo_OrdemServicoId",
                table: "NotificacoesEmail");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacoesEmail_EmpresaId_Tipo_OrdemServicoId",
                table: "NotificacoesEmail",
                columns: new[] { "EmpresaId", "Tipo", "OrdemServicoId" },
                unique: true);
        }
    }
}
