using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectorTrainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NominationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Organizer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ResultNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultRecordedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificatePublicId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CertificateFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CertificateSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectorTrainings_DirectorNominations_NominationId",
                        column: x => x.NominationId,
                        principalTable: "DirectorNominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorTrainings_NominationId",
                table: "DirectorTrainings",
                column: "NominationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectorTrainings");
        }
    }
}
