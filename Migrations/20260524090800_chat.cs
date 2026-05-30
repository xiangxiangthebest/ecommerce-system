using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecommerce_system.Migrations
{
    /// <inheritdoc />
    public partial class chat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatConversations_ChatConversationId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.RenameColumn(
                name: "ChatConversationId",
                table: "ChatMessages",
                newName: "ChatRoomId");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_ChatConversationId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_ChatRoomId");

            migrationBuilder.AlterColumn<string>(
                name: "MessageText",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "ProductsProductId",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatRoom",
                columns: table => new
                {
                    ChatRoomId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SellerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoom", x => x.ChatRoomId);
                    table.ForeignKey(
                        name: "FK_ChatRoom_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoom_Seller_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Seller",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ProductsProductId",
                table: "ChatMessages",
                column: "ProductsProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoom_CustomerId",
                table: "ChatRoom",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoom_SellerId",
                table: "ChatRoom",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatRoom_ChatRoomId",
                table: "ChatMessages",
                column: "ChatRoomId",
                principalTable: "ChatRoom",
                principalColumn: "ChatRoomId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Product_ProductsProductId",
                table: "ChatMessages",
                column: "ProductsProductId",
                principalTable: "Product",
                principalColumn: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatRoom_ChatRoomId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Product_ProductsProductId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatRoom");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ProductsProductId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ProductsProductId",
                table: "ChatMessages");

            migrationBuilder.RenameColumn(
                name: "ChatRoomId",
                table: "ChatMessages",
                newName: "ChatConversationId");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_ChatRoomId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_ChatConversationId");

            migrationBuilder.AlterColumn<string>(
                name: "MessageText",
                table: "ChatMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    ChatConversationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SellerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VariationJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.ChatConversationId);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Seller_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Seller",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_CustomerId",
                table: "ChatConversations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_ProductId",
                table: "ChatConversations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_SellerId",
                table: "ChatConversations",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatConversations_ChatConversationId",
                table: "ChatMessages",
                column: "ChatConversationId",
                principalTable: "ChatConversations",
                principalColumn: "ChatConversationId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
