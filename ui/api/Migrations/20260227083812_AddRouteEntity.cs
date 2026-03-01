using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    ActivityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RouteCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PointsJson = table.Column<string>(type: "TEXT", nullable: true),
                    WaypointsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PoisJson = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileJson = table.Column<string>(type: "TEXT", nullable: true),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGainM = table.Column<double>(type: "REAL", nullable: false),
                    ElevationLossM = table.Column<double>(type: "REAL", nullable: false),
                    MaxElevationM = table.Column<double>(type: "REAL", nullable: false),
                    MinElevationM = table.Column<double>(type: "REAL", nullable: false),
                    EstimatedTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RoutingProfile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceActivityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ActivityType",
                table: "Routes",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_CreatedAt",
                table: "Routes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Status",
                table: "Routes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Routes");
        }
    }
}
