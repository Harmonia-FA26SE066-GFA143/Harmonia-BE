using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLiturgicalWeekWithLiturgicalDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectorNotes_LiturgicalWeeks_WeekId",
                table: "DirectorNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_LiturgicalEvents_LiturgicalWeeks_WeekId",
                table: "LiturgicalEvents");

            migrationBuilder.DropTable(
                name: "LiturgicalWeeks");

            migrationBuilder.DropIndex(
                name: "IX_LiturgicalEvents_WeekId",
                table: "LiturgicalEvents");

            migrationBuilder.DropIndex(
                name: "IX_DirectorNotes_WeekId",
                table: "DirectorNotes");

            migrationBuilder.DropColumn(
                name: "WeekId",
                table: "LiturgicalEvents");

            migrationBuilder.DropColumn(
                name: "WeekId",
                table: "DirectorNotes");

            migrationBuilder.AddColumn<Guid>(
                name: "LiturgicalSeasonId",
                table: "LiturgicalEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "LiturgicalEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoteDate",
                table: "DirectorNotes",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiturgicalDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CelebrationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SeasonName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiturgicalDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEvents_LiturgicalSeasonId",
                table: "LiturgicalEvents",
                column: "LiturgicalSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalDays_Date",
                table: "LiturgicalDays",
                column: "Date",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LiturgicalEvents_LiturgicalSeasons_LiturgicalSeasonId",
                table: "LiturgicalEvents",
                column: "LiturgicalSeasonId",
                principalTable: "LiturgicalSeasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiturgicalEvents_LiturgicalSeasons_LiturgicalSeasonId",
                table: "LiturgicalEvents");

            migrationBuilder.DropTable(
                name: "LiturgicalDays");

            migrationBuilder.DropIndex(
                name: "IX_LiturgicalEvents_LiturgicalSeasonId",
                table: "LiturgicalEvents");

            migrationBuilder.DropColumn(
                name: "LiturgicalSeasonId",
                table: "LiturgicalEvents");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "LiturgicalEvents");

            migrationBuilder.DropColumn(
                name: "NoteDate",
                table: "DirectorNotes");

            migrationBuilder.AddColumn<Guid>(
                name: "WeekId",
                table: "LiturgicalEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "WeekId",
                table: "DirectorNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiturgicalWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LiturgicalSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeekEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiturgicalWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiturgicalWeeks_LiturgicalSeasons_LiturgicalSeasonId",
                        column: x => x.LiturgicalSeasonId,
                        principalTable: "LiturgicalSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEvents_WeekId",
                table: "LiturgicalEvents",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNotes_WeekId",
                table: "DirectorNotes",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalWeeks_LiturgicalSeasonId",
                table: "LiturgicalWeeks",
                column: "LiturgicalSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalWeeks_WeekStartDate",
                table: "LiturgicalWeeks",
                column: "WeekStartDate",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectorNotes_LiturgicalWeeks_WeekId",
                table: "DirectorNotes",
                column: "WeekId",
                principalTable: "LiturgicalWeeks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LiturgicalEvents_LiturgicalWeeks_WeekId",
                table: "LiturgicalEvents",
                column: "WeekId",
                principalTable: "LiturgicalWeeks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
