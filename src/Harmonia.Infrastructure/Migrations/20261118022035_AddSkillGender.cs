using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Level",
                table: "MemberSkills");

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Skills",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Skills");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "MemberSkills",
                type: "int",
                nullable: true);
        }
    }
}
