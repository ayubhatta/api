using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeBikeServiceAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMechanicModelsBookingToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mechanics_Bookings_IsAssignedTo",
                table: "Mechanics");

            migrationBuilder.DropIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_MechanicId",
                table: "Bookings",
                column: "MechanicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Mechanics_MechanicId",
                table: "Bookings",
                column: "MechanicId",
                principalTable: "Mechanics",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Mechanics_MechanicId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_MechanicId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics",
                column: "IsAssignedTo",
                unique: true,
                filter: "[IsAssignedTo] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Mechanics_Bookings_IsAssignedTo",
                table: "Mechanics",
                column: "IsAssignedTo",
                principalTable: "Bookings",
                principalColumn: "Id");
        }
    }
}
