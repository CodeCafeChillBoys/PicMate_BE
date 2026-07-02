using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteGraphers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_favorite_graphers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_favorite_graphers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_favorite_graphers_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_favorite_graphers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_favorite_graphers_GrapherProfileId",
                table: "user_favorite_graphers",
                column: "GrapherProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_user_favorite_graphers_UserId_GrapherProfileId",
                table: "user_favorite_graphers",
                columns: new[] { "UserId", "GrapherProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_favorite_graphers");
        }
    }
}
