using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { new Guid("6f10028c-f53c-4210-8d8f-b5cd70b0f1cf"), "Account" },
                    { new Guid("79377fbb-510c-4370-81b2-5cc7418ce2bc"), "Technical issue" },
                    { new Guid("df4f78ca-b46c-4c69-b573-5074c623ba1b"), "General" },
                    { new Guid("ed1c5f15-7408-4ba8-a984-70410da0903f"), "Billing" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("6f10028c-f53c-4210-8d8f-b5cd70b0f1cf"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("79377fbb-510c-4370-81b2-5cc7418ce2bc"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("df4f78ca-b46c-4c69-b573-5074c623ba1b"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("ed1c5f15-7408-4ba8-a984-70410da0903f"));
        }
    }
}
