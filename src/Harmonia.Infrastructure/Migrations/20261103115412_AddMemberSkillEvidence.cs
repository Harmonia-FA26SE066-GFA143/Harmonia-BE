using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSkillEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceFileName",
                table: "MemberSkills",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidencePublicId",
                table: "MemberSkills",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvidenceSizeBytes",
                table: "MemberSkills",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceUploadedAt",
                table: "MemberSkills",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceFileName",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "EvidencePublicId",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "EvidenceSizeBytes",
                table: "MemberSkills");

            migrationBuilder.DropColumn(
                name: "EvidenceUploadedAt",
                table: "MemberSkills");
        }
    }
}
