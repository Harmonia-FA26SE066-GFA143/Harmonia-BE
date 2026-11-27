using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranslateSkillCategorySeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChoirId",
                table: "SongLists",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChoirId",
                table: "Rehearsals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChoirId",
                table: "PracticeAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Choirs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Choirs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChoirMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChoirId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoirMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoirMemberships_Choirs_ChoirId",
                        column: x => x.ChoirId,
                        principalTable: "Choirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChoirMemberships_MemberProfiles_MemberId",
                        column: x => x.MemberId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiturgicalEventChoirs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChoirId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiturgicalEventChoirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiturgicalEventChoirs_Choirs_ChoirId",
                        column: x => x.ChoirId,
                        principalTable: "Choirs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiturgicalEventChoirs_LiturgicalEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "LiturgicalEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("0904ad8b-87f2-46c5-9938-f6cfbf0fa70b"),
                column: "Name",
                value: "Nhạc cụ");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("550a67bb-0393-4bc4-8fe0-d7a453871f81"),
                column: "Name",
                value: "Hát đơn ca");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("a1c322c5-14af-4ca4-892a-b6e8d0eacbbd"),
                column: "Name",
                value: "Đáp ca");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("c4829abd-bb1d-4c6c-b401-9a177a88e66a"),
                column: "Name",
                value: "Giọng hát");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("e2722720-3745-48ea-9e8e-14ec7a63907d"),
                column: "Name",
                value: "Hỗ trợ điều khiển");

            migrationBuilder.CreateIndex(
                name: "IX_SongLists_ChoirId",
                table: "SongLists",
                column: "ChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_Rehearsals_ChoirId",
                table: "Rehearsals",
                column: "ChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeAssignments_ChoirId",
                table: "PracticeAssignments",
                column: "ChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirMemberships_ChoirId",
                table: "ChoirMemberships",
                column: "ChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoirMemberships_MemberId_ChoirId",
                table: "ChoirMemberships",
                columns: new[] { "MemberId", "ChoirId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Choirs_Name",
                table: "Choirs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEventChoirs_ChoirId",
                table: "LiturgicalEventChoirs",
                column: "ChoirId");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEventChoirs_EventId_ChoirId",
                table: "LiturgicalEventChoirs",
                columns: new[] { "EventId", "ChoirId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeAssignments_Choirs_ChoirId",
                table: "PracticeAssignments",
                column: "ChoirId",
                principalTable: "Choirs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rehearsals_Choirs_ChoirId",
                table: "Rehearsals",
                column: "ChoirId",
                principalTable: "Choirs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SongLists_Choirs_ChoirId",
                table: "SongLists",
                column: "ChoirId",
                principalTable: "Choirs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PracticeAssignments_Choirs_ChoirId",
                table: "PracticeAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Rehearsals_Choirs_ChoirId",
                table: "Rehearsals");

            migrationBuilder.DropForeignKey(
                name: "FK_SongLists_Choirs_ChoirId",
                table: "SongLists");

            migrationBuilder.DropTable(
                name: "ChoirMemberships");

            migrationBuilder.DropTable(
                name: "LiturgicalEventChoirs");

            migrationBuilder.DropTable(
                name: "Choirs");

            migrationBuilder.DropIndex(
                name: "IX_SongLists_ChoirId",
                table: "SongLists");

            migrationBuilder.DropIndex(
                name: "IX_Rehearsals_ChoirId",
                table: "Rehearsals");

            migrationBuilder.DropIndex(
                name: "IX_PracticeAssignments_ChoirId",
                table: "PracticeAssignments");

            migrationBuilder.DropColumn(
                name: "ChoirId",
                table: "SongLists");

            migrationBuilder.DropColumn(
                name: "ChoirId",
                table: "Rehearsals");

            migrationBuilder.DropColumn(
                name: "ChoirId",
                table: "PracticeAssignments");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("0904ad8b-87f2-46c5-9938-f6cfbf0fa70b"),
                column: "Name",
                value: "Instrument");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("550a67bb-0393-4bc4-8fe0-d7a453871f81"),
                column: "Name",
                value: "Solo");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("a1c322c5-14af-4ca4-892a-b6e8d0eacbbd"),
                column: "Name",
                value: "Psalm");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("c4829abd-bb1d-4c6c-b401-9a177a88e66a"),
                column: "Name",
                value: "Vocal");

            migrationBuilder.UpdateData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("e2722720-3745-48ea-9e8e-14ec7a63907d"),
                column: "Name",
                value: "Conducting support");
        }
    }
}
