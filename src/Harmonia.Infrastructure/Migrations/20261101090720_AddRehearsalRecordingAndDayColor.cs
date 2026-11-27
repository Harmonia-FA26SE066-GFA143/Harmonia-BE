using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRehearsalRecordingAndDayColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "LiturgicalDays",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RehearsalRecordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RehearsalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SongId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AudioPublicId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalRecordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RehearsalRecordings_Rehearsals_RehearsalId",
                        column: x => x.RehearsalId,
                        principalTable: "Rehearsals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RehearsalRecordings_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecordings_RehearsalId_CreatedAt",
                table: "RehearsalRecordings",
                columns: new[] { "RehearsalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecordings_SongId",
                table: "RehearsalRecordings",
                column: "SongId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RehearsalRecordings");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "LiturgicalDays");
        }
    }
}
