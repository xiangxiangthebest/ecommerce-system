using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class deliveryfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryField_Customers_CustomerId",
                table: "DeliveryField");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "DeliveryField",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryField_CustomerId",
                table: "DeliveryField",
                newName: "IX_DeliveryField_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryField_Customers_UserId",
                table: "DeliveryField",
                column: "UserId",
                principalTable: "Customers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryField_Customers_UserId",
                table: "DeliveryField");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "DeliveryField",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryField_UserId",
                table: "DeliveryField",
                newName: "IX_DeliveryField_CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryField_Customers_CustomerId",
                table: "DeliveryField",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
