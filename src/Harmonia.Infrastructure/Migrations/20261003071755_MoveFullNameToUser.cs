using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Harmonia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveFullNameToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "MemberProfiles");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "SkillCategories",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("0904ad8b-87f2-46c5-9938-f6cfbf0fa70b"), null, true, "Instrument" },
                    { new Guid("550a67bb-0393-4bc4-8fe0-d7a453871f81"), null, true, "Solo" },
                    { new Guid("a1c322c5-14af-4ca4-892a-b6e8d0eacbbd"), null, true, "Psalm" },
                    { new Guid("c4829abd-bb1d-4c6c-b401-9a177a88e66a"), null, true, "Vocal" },
                    { new Guid("e2722720-3745-48ea-9e8e-14ec7a63907d"), null, true, "Conducting support" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("0904ad8b-87f2-46c5-9938-f6cfbf0fa70b"));

            migrationBuilder.DeleteData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("550a67bb-0393-4bc4-8fe0-d7a453871f81"));

            migrationBuilder.DeleteData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("a1c322c5-14af-4ca4-892a-b6e8d0eacbbd"));

            migrationBuilder.DeleteData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("c4829abd-bb1d-4c6c-b401-9a177a88e66a"));

            migrationBuilder.DeleteData(
                table: "SkillCategories",
                keyColumn: "Id",
                keyValue: new Guid("e2722720-3745-48ea-9e8e-14ec7a63907d"));

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "MemberProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
