using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ActivityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGainM = table.Column<double>(type: "REAL", nullable: false),
                    ElevationLossM = table.Column<double>(type: "REAL", nullable: false),
                    MovingTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    GpxFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StatsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiReportJson = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExternalUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WebhookSubscriptionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Source_ExternalId",
                table: "Activities",
                columns: new[] { "Source", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_StartTime",
                table: "Activities",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Status",
                table: "Activities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_Provider",
                table: "Integrations",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Integrations");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
