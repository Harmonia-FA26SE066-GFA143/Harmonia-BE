using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowRedeclareRejectedMemberSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills",
                columns: new[] { "MemberId", "SkillId" },
                unique: true,
                filter: "[Status] <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills",
                columns: new[] { "MemberId", "SkillId" },
                unique: true);
        }
    }
}
