using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRosterAssignmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByAssignmentId",
                table: "RosterAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "RosterAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RosterAssignments_ReplacedByAssignmentId",
                table: "RosterAssignments",
                column: "ReplacedByAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_RosterAssignments_RosterAssignments_ReplacedByAssignmentId",
                table: "RosterAssignments",
                column: "ReplacedByAssignmentId",
                principalTable: "RosterAssignments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RosterAssignments_RosterAssignments_ReplacedByAssignmentId",
                table: "RosterAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RosterAssignments_ReplacedByAssignmentId",
                table: "RosterAssignments");

            migrationBuilder.DropColumn(
                name: "ReplacedByAssignmentId",
                table: "RosterAssignments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RosterAssignments");
        }
    }
}
