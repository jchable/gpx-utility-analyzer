using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRacePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NutritionProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CaloriesKcal = table.Column<double>(type: "REAL", nullable: false),
                    CarbsG = table.Column<double>(type: "REAL", nullable: false),
                    ProteinsG = table.Column<double>(type: "REAL", nullable: true),
                    FatsG = table.Column<double>(type: "REAL", nullable: true),
                    SodiumMg = table.Column<double>(type: "REAL", nullable: true),
                    CaffeineG = table.Column<double>(type: "REAL", nullable: true),
                    WeightG = table.Column<double>(type: "REAL", nullable: true),
                    VolumeML = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionProducts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RacePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    ActivityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RouteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PointsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileJson = table.Column<string>(type: "TEXT", nullable: true),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGainM = table.Column<double>(type: "REAL", nullable: false),
                    ElevationLossM = table.Column<double>(type: "REAL", nullable: false),
                    MaxElevationM = table.Column<double>(type: "REAL", nullable: false),
                    MinElevationM = table.Column<double>(type: "REAL", nullable: false),
                    RaceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    StartLatitude = table.Column<double>(type: "REAL", nullable: true),
                    StartLongitude = table.Column<double>(type: "REAL", nullable: true),
                    TargetTimeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetTimeBSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetTimeCSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    PerformanceCoefficient = table.Column<double>(type: "REAL", nullable: false),
                    EquipmentJson = table.Column<string>(type: "TEXT", nullable: true),
                    ShareToken = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    LinkedActivityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacePlans_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RacePlanCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RacePlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ElevationM = table.Column<double>(type: "REAL", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    CutoffTimeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetArrivalSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    PlannedPauseSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    IsCrewAccessible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CrewNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    HasDropBag = table.Column<bool>(type: "INTEGER", nullable: false),
                    DropBagContentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    EquipmentTakeJson = table.Column<string>(type: "TEXT", nullable: true),
                    EquipmentLeaveJson = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacePlanCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacePlanCheckpoints_RacePlans_RacePlanId",
                        column: x => x.RacePlanId,
                        principalTable: "RacePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RacePlanNutritionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RacePlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AtCheckpointId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FromCheckpointId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToCheckpointId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Quantity = table.Column<double>(type: "REAL", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TimeOffsetSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacePlanNutritionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacePlanNutritionItems_NutritionProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "NutritionProducts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RacePlanNutritionItems_RacePlanCheckpoints_AtCheckpointId",
                        column: x => x.AtCheckpointId,
                        principalTable: "RacePlanCheckpoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RacePlanNutritionItems_RacePlanCheckpoints_FromCheckpointId",
                        column: x => x.FromCheckpointId,
                        principalTable: "RacePlanCheckpoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RacePlanNutritionItems_RacePlanCheckpoints_ToCheckpointId",
                        column: x => x.ToCheckpointId,
                        principalTable: "RacePlanCheckpoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RacePlanNutritionItems_RacePlans_RacePlanId",
                        column: x => x.RacePlanId,
                        principalTable: "RacePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NutritionProducts_UserId_Type",
                table: "NutritionProducts",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanCheckpoints_RacePlanId_Order",
                table: "RacePlanCheckpoints",
                columns: new[] { "RacePlanId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanNutritionItems_AtCheckpointId",
                table: "RacePlanNutritionItems",
                column: "AtCheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanNutritionItems_FromCheckpointId",
                table: "RacePlanNutritionItems",
                column: "FromCheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanNutritionItems_ProductId",
                table: "RacePlanNutritionItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanNutritionItems_RacePlanId",
                table: "RacePlanNutritionItems",
                column: "RacePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlanNutritionItems_ToCheckpointId",
                table: "RacePlanNutritionItems",
                column: "ToCheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlans_CreatedAt",
                table: "RacePlans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlans_ShareToken",
                table: "RacePlans",
                column: "ShareToken");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlans_Status",
                table: "RacePlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RacePlans_UserId_UpdatedAt",
                table: "RacePlans",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RacePlanNutritionItems");

            migrationBuilder.DropTable(
                name: "NutritionProducts");

            migrationBuilder.DropTable(
                name: "RacePlanCheckpoints");

            migrationBuilder.DropTable(
                name: "RacePlans");
        }
    }
}
