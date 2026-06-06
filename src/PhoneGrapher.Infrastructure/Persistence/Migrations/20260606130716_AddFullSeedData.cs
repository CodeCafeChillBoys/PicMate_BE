using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFullSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "grapher_activity_areas",
                columns: new[] { "Id", "City", "CreatedAt", "District", "GrapherProfileId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000002"), "TP.HCM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Quận 1", new Guid("30000000-0000-0000-0000-000000000001"), null },
                    { new Guid("60000000-0000-0000-0000-000000000003"), "TP.HCM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Quận 3", new Guid("30000000-0000-0000-0000-000000000001"), null }
                });

            migrationBuilder.UpdateData(
                table: "grapher_portfolio_items",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "Caption",
                value: "Mùa thu Hà Nội");

            migrationBuilder.UpdateData(
                table: "grapher_profiles",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "District",
                value: "Quận 1");

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "AvatarUrl", "CreatedAt", "Email", "FullName", "IsActive", "PasswordHash", "PhoneNumber", "Role", "UpdatedAt" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000002"), "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=96&h=96&fit=crop", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "customer@picmate.vn", "Trần Bình", true, "seeded-user-register-again-to-login", "0900000002", "Customer", null });

            migrationBuilder.InsertData(
                table: "bookings",
                columns: new[] { "Id", "CancellationReason", "CompletedAt", "CreatedAt", "CustomerId", "DurationMinutes", "GrapherPayoutAmount", "GrapherProfileId", "Location", "Note", "PlatformFeeAmount", "ScheduledAt", "ServicePackageId", "Status", "TotalAmount", "UpdatedAt" },
                values: new object[] { new Guid("70000000-0000-0000-0000-000000000001"), null, new DateTimeOffset(new DateTime(2026, 1, 3, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000002"), 60, 135000m, new Guid("30000000-0000-0000-0000-000000000001"), "Đường sách Nguyễn Văn Bình, Quận 1, TP.HCM", "Mang theo phụ kiện vintage nhé", 15000m, new DateTimeOffset(new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000001"), "Completed", 150000m, null });

            migrationBuilder.InsertData(
                table: "messages",
                columns: new[] { "Id", "Content", "CreatedAt", "IsRead", "ReceiverId", "SenderId", "UpdatedAt" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000001"), "Chào bạn, mình muốn book lịch chụp cuối tuần này", new DateTimeOffset(new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new Guid("20000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000002"), null });

            migrationBuilder.InsertData(
                table: "payment_transactions",
                columns: new[] { "Id", "Amount", "BookingId", "CreatedAt", "EscrowStatus", "GrapherPayoutAmount", "PaidAt", "PlatformFeeAmount", "Provider", "ProviderResponseCode", "ProviderTransactionId", "RawCallbackPayload", "ReleasedAt", "Status", "TransactionCode", "UpdatedAt" },
                values: new object[] { new Guid("80000000-0000-0000-0000-000000000001"), 150000m, new Guid("70000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Released", 135000m, new DateTimeOffset(new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 15000m, "VnPay", null, "VNPAY_987654321", null, new DateTimeOffset(new DateTime(2026, 1, 3, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Succeeded", "TXN_123456789", null });

            migrationBuilder.InsertData(
                table: "reviews",
                columns: new[] { "Id", "BookingId", "Comment", "CreatedAt", "CustomerId", "GrapherProfileId", "Rating", "UpdatedAt" },
                values: new object[] { new Guid("90000000-0000-0000-0000-000000000001"), new Guid("70000000-0000-0000-0000-000000000001"), "Nháy rất nhiệt tình, ảnh đẹp và gửi nhanh chóng. Sẽ ủng hộ lại!", new DateTimeOffset(new DateTime(2026, 1, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("20000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000001"), 5, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "grapher_activity_areas",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "grapher_activity_areas",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "messages",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "payment_transactions",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "reviews",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "bookings",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "grapher_portfolio_items",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "Caption",
                value: null);

            migrationBuilder.UpdateData(
                table: "grapher_profiles",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "District",
                value: null);
        }
    }
}
