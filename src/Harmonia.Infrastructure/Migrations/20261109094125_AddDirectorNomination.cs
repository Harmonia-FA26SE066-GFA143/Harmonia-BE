using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorNomination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectorNominations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NominatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProposedChoirId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrainingWaiverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppointedChoirId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorNominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectorNominations_Choirs_AppointedChoirId",
                        column: x => x.AppointedChoirId,
                        principalTable: "Choirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectorNominations_Choirs_ProposedChoirId",
                        column: x => x.ProposedChoirId,
                        principalTable: "Choirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectorNominations_MemberProfiles_MemberId",
                        column: x => x.MemberId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectorNominations_Users_NominatedById",
                        column: x => x.NominatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectorNominations_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNominations_AppointedChoirId",
                table: "DirectorNominations",
                column: "AppointedChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNominations_MemberId_Status",
                table: "DirectorNominations",
                columns: new[] { "MemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNominations_NominatedById",
                table: "DirectorNominations",
                column: "NominatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNominations_ProposedChoirId",
                table: "DirectorNominations",
                column: "ProposedChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorNominations_ReviewedById",
                table: "DirectorNominations",
                column: "ReviewedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectorNominations");
        }
    }
}
