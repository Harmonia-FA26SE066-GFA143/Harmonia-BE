using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChoirDirector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DirectorId",
                table: "Choirs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Choirs_DirectorId",
                table: "Choirs",
                column: "DirectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Choirs_Users_DirectorId",
                table: "Choirs",
                column: "DirectorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Choirs_Users_DirectorId",
                table: "Choirs");

            migrationBuilder.DropIndex(
                name: "IX_Choirs_DirectorId",
                table: "Choirs");

            migrationBuilder.DropColumn(
                name: "DirectorId",
                table: "Choirs");
        }
    }
}
