using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class bzd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Customers_CartId",
                table: "Customers",
                column: "CartId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Cart_CartId",
                table: "Customers",
                column: "CartId",
                principalTable: "Cart",
                principalColumn: "CartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Cart_CartId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CartId",
                table: "Customers");
        }
    }
}
