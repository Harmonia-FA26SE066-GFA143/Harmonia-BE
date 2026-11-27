using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipationLastReminderAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderAt",
                table: "EventParticipations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReminderAt",
                table: "EventParticipations");
        }
    }
}
