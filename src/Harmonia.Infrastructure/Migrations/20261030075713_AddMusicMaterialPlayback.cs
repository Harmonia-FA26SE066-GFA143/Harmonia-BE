using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicMaterialPlayback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaybackAudioPublicId",
                table: "MusicMaterials",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlaybackDurationSeconds",
                table: "MusicMaterials",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaybackInstrument",
                table: "MusicMaterials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaybackNotesPublicId",
                table: "MusicMaterials",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackAudioPublicId",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "PlaybackDurationSeconds",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "PlaybackInstrument",
                table: "MusicMaterials");

            migrationBuilder.DropColumn(
                name: "PlaybackNotesPublicId",
                table: "MusicMaterials");
        }
    }
}
