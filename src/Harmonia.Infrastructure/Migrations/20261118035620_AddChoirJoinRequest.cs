using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChoirJoinRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreferredChoirId",
                table: "MemberRegistrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChoirJoinRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChoirId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsInvitation = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoirJoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoirJoinRequests_Choirs_ChoirId",
                        column: x => x.ChoirId,
                        principalTable: "Choirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoirJoinRequests_MemberProfiles_MemberId",
                        column: x => x.MemberId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoirJoinRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistrations_PreferredChoirId",
                table: "MemberRegistrations",
                column: "PreferredChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirMemberships_MemberId_Active",
                table: "ChoirMemberships",
                columns: new[] { "MemberId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirJoinRequests_ChoirId_Status",
                table: "ChoirJoinRequests",
                columns: new[] { "ChoirId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoirJoinRequests_MemberId_ChoirId",
                table: "ChoirJoinRequests",
                columns: new[] { "MemberId", "ChoirId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirJoinRequests_MemberId_PendingRequest",
                table: "ChoirJoinRequests",
                column: "MemberId",
                unique: true,
                filter: "[Status] = 0 AND [IsInvitation] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirJoinRequests_ReviewedById",
                table: "ChoirJoinRequests",
                column: "ReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberRegistrations_Choirs_PreferredChoirId",
                table: "MemberRegistrations",
                column: "PreferredChoirId",
                principalTable: "Choirs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberRegistrations_Choirs_PreferredChoirId",
                table: "MemberRegistrations");

            migrationBuilder.DropTable(
                name: "ChoirJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_MemberRegistrations_PreferredChoirId",
                table: "MemberRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_ChoirMemberships_MemberId_Active",
                table: "ChoirMemberships");

            migrationBuilder.DropColumn(
                name: "PreferredChoirId",
                table: "MemberRegistrations");
        }
    }
}
