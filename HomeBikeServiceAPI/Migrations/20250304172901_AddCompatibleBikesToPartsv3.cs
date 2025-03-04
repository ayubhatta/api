using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeBikeServiceAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCompatibleBikesToPartsv3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompatibleBikes",
                table: "BikeParts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibleBikes",
                table: "BikeParts");
        }
    }
}
