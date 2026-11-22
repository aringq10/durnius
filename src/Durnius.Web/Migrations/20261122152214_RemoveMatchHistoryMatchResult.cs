using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Durnius.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMatchHistoryMatchResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchResult",
                table: "MatchHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatchResult",
                table: "MatchHistories",
                type: "integer",
                nullable: true);
        }
    }
}
