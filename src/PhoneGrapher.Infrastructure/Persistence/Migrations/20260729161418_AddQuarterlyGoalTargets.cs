using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuarterlyGoalTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompletionRateTarget",
                table: "system_settings",
                type: "numeric",
                nullable: false,
                defaultValue: 90m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuarterlyRevenueTarget",
                table: "system_settings",
                type: "numeric",
                nullable: false,
                defaultValue: 50000000m);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedGrapherTarget",
                table: "system_settings",
                type: "integer",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionRateTarget",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "QuarterlyRevenueTarget",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "VerifiedGrapherTarget",
                table: "system_settings");
        }
    }
}
