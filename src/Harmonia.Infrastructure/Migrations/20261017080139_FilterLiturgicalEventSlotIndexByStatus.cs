using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterLiturgicalEventSlotIndexByStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LiturgicalEvents_EventDate_Time_LocationId",
                table: "LiturgicalEvents");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEvents_EventDate_Time_LocationId",
                table: "LiturgicalEvents",
                columns: new[] { "EventDate", "Time", "LocationId" },
                unique: true,
                filter: "[Status] <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LiturgicalEvents_EventDate_Time_LocationId",
                table: "LiturgicalEvents");

            migrationBuilder.CreateIndex(
                name: "IX_LiturgicalEvents_EventDate_Time_LocationId",
                table: "LiturgicalEvents",
                columns: new[] { "EventDate", "Time", "LocationId" },
                unique: true);
        }
    }
}
