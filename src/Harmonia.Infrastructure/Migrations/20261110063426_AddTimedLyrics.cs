using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimedLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AudioDurationSeconds",
                table: "MusicMaterials",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileRevision",
                table: "MusicMaterials",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PlaybackNotesSha256",
                table: "MusicMaterials",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimedLyrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudioMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LyricsMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudioRevision = table.Column<int>(type: "int", nullable: false),
                    Granularity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Lines = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimedLyrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimedLyrics_MusicMaterials_AudioMaterialId",
                        column: x => x.AudioMaterialId,
                        principalTable: "MusicMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimedLyrics_MusicMaterials_LyricsMaterialId",
                        column: x => x.LyricsMaterialId,
                        principalTable: "MusicMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimedLyrics_AudioMaterialId",
                table: "TimedLyrics",
                column: "AudioMaterialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimedLyrics_LyricsMaterialId",
                table: "TimedLyrics",
                column: "LyricsMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimedLyrics");

            migrationBuilder.DropColumn(
                name: "AudioDurationSeconds",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "FileRevision",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "PlaybackNotesSha256",
                table: "MusicMaterials");
        }
    }
}
