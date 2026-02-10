using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpxAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAndTrack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileJson",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackGeoJson",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileJson",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TrackGeoJson",
                table: "Activities");
        }
    }
}
