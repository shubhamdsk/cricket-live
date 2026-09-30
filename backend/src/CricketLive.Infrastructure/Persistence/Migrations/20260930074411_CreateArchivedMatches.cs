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
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:case_insensitive", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.CreateTable(
                name: "archived_matches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeriesId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SeriesName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, collation: "case_insensitive"),
                    HomeTeamId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AwayTeamId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HomeTeamName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AwayTeamName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archived_matches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_away_team",
                table: "archived_matches",
                columns: new[] { "AwayTeamId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_home_team",
                table: "archived_matches",
                columns: new[] { "HomeTeamId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_series",
                table: "archived_matches",
                column: "SeriesName");

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_series_id",
                table: "archived_matches",
                columns: new[] { "SeriesId", "StartTimeUtc" });

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
