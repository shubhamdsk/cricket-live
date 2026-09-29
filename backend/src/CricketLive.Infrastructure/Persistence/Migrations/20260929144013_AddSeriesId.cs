using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeriesId",
                table: "archived_matches",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_series_id",
                table: "archived_matches",
                columns: new[] { "SeriesId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_archived_matches_series_id",
                table: "archived_matches");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "archived_matches");
        }
    }
}
