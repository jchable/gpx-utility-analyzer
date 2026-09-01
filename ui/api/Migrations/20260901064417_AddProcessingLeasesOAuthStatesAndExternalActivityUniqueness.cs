using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingLeasesOAuthStatesAndExternalActivityUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingLeaseExpiresAt",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingLeaseId",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OAuthStates",
                columns: table => new
                {
                    Nonce = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthStates", x => x.Nonce);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthStates");

            migrationBuilder.DropIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseExpiresAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseId",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities",
                columns: new[] { "Source", "ExternalId" });
        }
    }
}
