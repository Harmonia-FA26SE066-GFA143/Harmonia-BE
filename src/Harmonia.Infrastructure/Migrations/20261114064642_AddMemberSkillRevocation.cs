using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSkillRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills");

            migrationBuilder.AddColumn<string>(
                name: "RevokeReason",
                table: "MemberSkills",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "MemberSkills",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedBy",
                table: "MemberSkills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills",
                columns: new[] { "MemberId", "SkillId" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSkills_RevokedBy",
                table: "MemberSkills",
                column: "RevokedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberSkills_Users_RevokedBy",
                table: "MemberSkills",
                column: "RevokedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberSkills_Users_RevokedBy",
                table: "MemberSkills");

            migrationBuilder.DropIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills");

            migrationBuilder.DropIndex(
                name: "IX_MemberSkills_RevokedBy",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "RevokeReason",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "RevokedBy",
                table: "MemberSkills");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSkills_MemberId_SkillId",
                table: "MemberSkills",
                columns: new[] { "MemberId", "SkillId" },
                unique: true,
                filter: "[Status] <> 2");
        }
    }
}
