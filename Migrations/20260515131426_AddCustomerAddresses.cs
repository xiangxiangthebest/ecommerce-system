using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DeliveryField",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "DeliveryField",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "DeliveryField",
                newName: "RecipientName");

            migrationBuilder.RenameColumn(
                name: "DeliveryFieldId",
                table: "DeliveryField",
                newName: "CustomerId");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "DeliveryField",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "DeliveryField",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "DeliveryField",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "DeliveryField",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "DeliveryField",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DeliveryField",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Postcode",
                table: "DeliveryField",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeliveryField",
                table: "DeliveryField",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryField_CustomerId",
                table: "DeliveryField",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryField_Customers_CustomerId",
                table: "DeliveryField",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryField_Customers_CustomerId",
                table: "DeliveryField");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeliveryField",
                table: "DeliveryField");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryField_CustomerId",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "City",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DeliveryField");

            migrationBuilder.DropColumn(
                name: "Postcode",
                table: "DeliveryField");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "DeliveryField",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "RecipientName",
                table: "DeliveryField",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "DeliveryField",
                newName: "DeliveryFieldId");

            migrationBuilder.AlterColumn<int>(
                name: "DeliveryFieldId",
                table: "DeliveryField",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeliveryField",
                table: "DeliveryField",
                column: "DeliveryFieldId");
        }
    }
}
