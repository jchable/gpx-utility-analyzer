using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalorieMethod",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedCalories",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedExertion",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalorieMethod",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EstimatedCalories",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PerceivedExertion",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SessionType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Activities");
        }
    }
}
