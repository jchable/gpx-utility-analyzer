using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingLeasesOAuthStatesAndPerUserExternalActivityUniqueness : Migration
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

            // Data migration — EF cannot scaffold this, and without it CreateIndex
            // below fails with "UNIQUE constraint failed" on every database that
            // already holds the duplicates the index is being introduced to prevent.
            //
            // Keeps the OLDEST row of each (UserId, Source, ExternalId) group and
            // deletes the newer ones; Id breaks a CreatedAt tie so the choice is
            // deterministic. Uploads carry a NULL ExternalId and are never grouped.
            // Portable between SQLite and PostgreSQL (quoted identifiers, correlated
            // EXISTS against the outer table by name).
            //
            // ExternalActivityDeduplication logs the rows this removes, from the
            // startup path, just before the migration is applied.
            migrationBuilder.Sql(
                """
                DELETE FROM "Activities"
                WHERE "ExternalId" IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM "Activities" AS "older"
                      WHERE "older"."UserId"     = "Activities"."UserId"
                        AND "older"."Source"     = "Activities"."Source"
                        AND "older"."ExternalId" = "Activities"."ExternalId"
                        AND ("older"."CreatedAt" < "Activities"."CreatedAt"
                          OR ("older"."CreatedAt" = "Activities"."CreatedAt"
                              AND "older"."Id" < "Activities"."Id"))
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_Source_ExternalId",
                table: "Activities",
                columns: new[] { "UserId", "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthStates");

            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_Source_ExternalId",
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
