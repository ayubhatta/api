using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeBikeServiceAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicIdToBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics");

            migrationBuilder.AddColumn<int>(
                name: "MechanicId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics",
                column: "IsAssignedTo",
                unique: true,
                filter: "[IsAssignedTo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "MechanicId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Mechanics_IsAssignedTo",
                table: "Mechanics",
                column: "IsAssignedTo");
        }
    }
}
