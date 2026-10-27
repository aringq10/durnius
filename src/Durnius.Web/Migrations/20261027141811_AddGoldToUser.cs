using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Durnius.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Gold",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gold",
                table: "Users");
        }
    }
}
