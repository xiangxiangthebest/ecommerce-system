using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveredAtToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Order",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnApprovedAt",
                table: "Order",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnType",
                table: "Order",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "ReturnApprovedAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "ReturnType",
                table: "Order");
        }
    }
}
