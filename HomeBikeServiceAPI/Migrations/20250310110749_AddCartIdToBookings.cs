using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeBikeServiceAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCartIdToBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CartId",
                table: "Bookings",
                column: "CartId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Carts_CartId",
                table: "Bookings",
                column: "CartId",
                principalTable: "Carts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Carts_CartId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CartId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CartId",
                table: "Bookings");
        }
    }
}
