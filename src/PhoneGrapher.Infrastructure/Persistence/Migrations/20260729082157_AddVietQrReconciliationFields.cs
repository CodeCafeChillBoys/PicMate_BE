using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVietQrReconciliationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerClaimedAt",
                table: "payment_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "payment_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "payment_transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "payment_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_Provider_Status",
                table: "payment_transactions",
                columns: new[] { "Provider", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_transactions_Provider_Status",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "CustomerClaimedAt",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "payment_transactions");
        }
    }
}
