using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class AddDateJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateJoin",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "datetime('now')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateJoin",
                table: "Users");
        }
    }
}
