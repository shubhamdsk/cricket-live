using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateBackfillState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backfill_state",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NextOffset = table.Column<int>(type: "integer", nullable: false),
                    PagesReadOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PagesReadToday = table.Column<int>(type: "integer", nullable: false),
                    LapsCompleted = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backfill_state", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backfill_state");
        }
    }
}
