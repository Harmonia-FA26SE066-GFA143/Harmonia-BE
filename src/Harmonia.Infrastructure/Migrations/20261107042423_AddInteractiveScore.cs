using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractiveScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaybackAudioSha256",
                table: "MusicMaterials",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreRevision",
                table: "MusicMaterials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoreSha256",
                table: "MusicMaterials",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackAudioSha256",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "ScoreRevision",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "ScoreSha256",
                table: "MusicMaterials");
        }
    }
}
