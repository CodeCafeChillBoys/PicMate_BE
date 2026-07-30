using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlsToReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "reviews",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "reviews");
        }
    }
}
