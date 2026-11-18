using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventChoirSubstituteDirector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "LiturgicalEventChoirs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteDirectorId",
                table: "LiturgicalEventChoirs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEventChoirs_SubstituteDirectorId",
                table: "LiturgicalEventChoirs",
                column: "SubstituteDirectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_LiturgicalEventChoirs_Users_SubstituteDirectorId",
                table: "LiturgicalEventChoirs",
                column: "SubstituteDirectorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiturgicalEventChoirs_Users_SubstituteDirectorId",
                table: "LiturgicalEventChoirs");

            migrationBuilder.DropIndex(
                name: "IX_LiturgicalEventChoirs_SubstituteDirectorId",
                table: "LiturgicalEventChoirs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "LiturgicalEventChoirs");

            migrationBuilder.DropColumn(
                name: "SubstituteDirectorId",
                table: "LiturgicalEventChoirs");
        }
    }
}
