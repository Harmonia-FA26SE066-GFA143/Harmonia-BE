using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentMaterialsAndNameTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiturgicalNameTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VietnameseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiturgicalNameTranslations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PracticeAssignmentMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeAssignmentMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeAssignmentMaterials_MusicMaterials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "MusicMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PracticeAssignmentMaterials_PracticeAssignments_PracticeAssignmentId",
                        column: x => x.PracticeAssignmentId,
                        principalTable: "PracticeAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalNameTranslations_Kind_EnglishName",
                table: "LiturgicalNameTranslations",
                columns: new[] { "Kind", "EnglishName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeAssignmentMaterials_MaterialId",
                table: "PracticeAssignmentMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeAssignmentMaterials_PracticeAssignmentId_MaterialId",
                table: "PracticeAssignmentMaterials",
                columns: new[] { "PracticeAssignmentId", "MaterialId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiturgicalNameTranslations");

            migrationBuilder.DropTable(
                name: "PracticeAssignmentMaterials");
        }
    }
}
