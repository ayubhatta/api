using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeBikeServiceAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBikePartsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibleBikes",
                table: "BikeParts");

            migrationBuilder.AddColumn<string>(
                name: "CompatibleBikesJson",
                table: "BikeParts",
                type: "NVARCHAR(MAX)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibleBikesJson",
                table: "BikeParts");

            migrationBuilder.AddColumn<string>(
                name: "CompatibleBikes",
                table: "BikeParts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
