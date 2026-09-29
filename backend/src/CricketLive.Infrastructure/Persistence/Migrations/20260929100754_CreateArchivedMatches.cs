using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateArchivedMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archived_matches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeriesName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archived_matches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_series",
                table: "archived_matches",
                column: "SeriesName");

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_start_time",
                table: "archived_matches",
                column: "StartTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archived_matches");
        }
    }
}
