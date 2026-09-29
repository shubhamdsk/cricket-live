using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwayTeamId",
                table: "archived_matches",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AwayTeamName",
                table: "archived_matches",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HomeTeamId",
                table: "archived_matches",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HomeTeamName",
                table: "archived_matches",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Backfilled rather than left empty, which is what SeriesId had to settle for. The
            // payload already holds both sides, so the values are read back out of it instead of
            // history being written off: without this, every match archived before today would be
            // absent from every team page, and a team's first match would appear to be whenever
            // these columns happened to be added.
            //
            // json_extract is SQLite's, and this is the second provider-specific line in the
            // project after the NOCASE collation. PostgreSQL spells the same thing
            // Payload::json -> 'home' -> 'team' ->> 'id'. It is confined to a migration, which is
            // the one place a provider's SQL dialect is expected to show.
            migrationBuilder.Sql(
                """
                UPDATE archived_matches
                SET HomeTeamId   = COALESCE(json_extract(Payload, '$.home.team.id'), ''),
                    AwayTeamId   = COALESCE(json_extract(Payload, '$.away.team.id'), ''),
                    HomeTeamName = COALESCE(json_extract(Payload, '$.home.team.name'), ''),
                    AwayTeamName = COALESCE(json_extract(Payload, '$.away.team.name'), '')
                """);

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_away_team",
                table: "archived_matches",
                columns: new[] { "AwayTeamId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_archived_matches_home_team",
                table: "archived_matches",
                columns: new[] { "HomeTeamId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_archived_matches_away_team",
                table: "archived_matches");

            migrationBuilder.DropIndex(
                name: "ix_archived_matches_home_team",
                table: "archived_matches");

            migrationBuilder.DropColumn(
                name: "AwayTeamId",
                table: "archived_matches");

            migrationBuilder.DropColumn(
                name: "AwayTeamName",
                table: "archived_matches");

            migrationBuilder.DropColumn(
                name: "HomeTeamId",
                table: "archived_matches");

            migrationBuilder.DropColumn(
                name: "HomeTeamName",
                table: "archived_matches");
        }
    }
}
