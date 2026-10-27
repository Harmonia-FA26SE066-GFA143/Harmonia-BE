using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongClassificationAndRosterTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RosterShortages");

            migrationBuilder.AddColumn<int>(
                name: "RequiredCount",
                table: "SongVocalRequirements",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "SongThemes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredCount",
                table: "SongInstrumentRequirements",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "WholeServiceFinalizedAt",
                table: "ServiceRosters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "RosterAssignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventPersonnelRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPersonnelRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPersonnelRequirements_LiturgicalEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "LiturgicalEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventPersonnelRequirements_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LiturgicalSlotRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiturgicalSlotRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiturgicalSlotRequirements_LiturgicalSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "LiturgicalSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiturgicalSlotRequirements_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SongThemes_ParentId",
                table: "SongThemes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPersonnelRequirements_EventId_SkillId",
                table: "EventPersonnelRequirements",
                columns: new[] { "EventId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventPersonnelRequirements_SkillId",
                table: "EventPersonnelRequirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalSlotRequirements_SkillId",
                table: "LiturgicalSlotRequirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalSlotRequirements_SlotId_SkillId",
                table: "LiturgicalSlotRequirements",
                columns: new[] { "SlotId", "SkillId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SongThemes_SongThemes_ParentId",
                table: "SongThemes",
                column: "ParentId",
                principalTable: "SongThemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SongThemes_SongThemes_ParentId",
                table: "SongThemes");

            migrationBuilder.DropTable(
                name: "EventPersonnelRequirements");

            migrationBuilder.DropTable(
                name: "LiturgicalSlotRequirements");

            migrationBuilder.DropIndex(
                name: "IX_SongThemes_ParentId",
                table: "SongThemes");

            migrationBuilder.DropColumn(
                name: "RequiredCount",
                table: "SongVocalRequirements");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "SongThemes");

            migrationBuilder.DropColumn(
                name: "RequiredCount",
                table: "SongInstrumentRequirements");

            migrationBuilder.DropColumn(
                name: "WholeServiceFinalizedAt",
                table: "ServiceRosters");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "RosterAssignments");

            migrationBuilder.CreateTable(
                name: "RosterShortages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RosterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableCount = table.Column<int>(type: "int", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterShortages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterShortages_ServiceRosters_RosterId",
                        column: x => x.RosterId,
                        principalTable: "ServiceRosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterShortages_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RosterShortages_RosterId",
                table: "RosterShortages",
                column: "RosterId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterShortages_SkillId",
                table: "RosterShortages",
                column: "SkillId");
        }
    }
}
