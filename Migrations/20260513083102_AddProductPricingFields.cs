using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Product",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OriginalPrice",
                table: "Product",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ReviewCount",
                table: "Product",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Birthday",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Product");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Birthday",
                table: "Customers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");
        }
    }
}
