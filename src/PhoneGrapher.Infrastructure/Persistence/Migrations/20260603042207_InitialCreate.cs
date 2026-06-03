using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "presets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DownloadUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "style_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_style_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grapher_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CccdNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CccdFrontImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CccdBackImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    KycStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    AverageRating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grapher_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grapher_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grapher_activity_areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grapher_activity_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grapher_activity_areas_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grapher_portfolio_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grapher_portfolio_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grapher_portfolio_items_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grapher_service_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grapher_service_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grapher_service_packages_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grapher_style_tags",
                columns: table => new
                {
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StyleTagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grapher_style_tags", x => new { x.GrapherProfileId, x.StyleTagId });
                    table.ForeignKey(
                        name: "FK_grapher_style_tags_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grapher_style_tags_style_tags_StyleTagId",
                        column: x => x.StyleTagId,
                        principalTable: "style_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrapherPayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_grapher_service_packages_ServicePackageId",
                        column: x => x.ServicePackageId,
                        principalTable: "grapher_service_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EscrowStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TransactionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderResponseCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrapherPayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RawCallbackPayload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrapherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.Id);
                    table.CheckConstraint("ck_reviews_rating_range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_reviews_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reviews_grapher_profiles_GrapherProfileId",
                        column: x => x.GrapherProfileId,
                        principalTable: "grapher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reviews_users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "presets",
                columns: new[] { "Id", "Category", "CreatedAt", "DownloadCount", "DownloadUrl", "ImageUrl", "IsActive", "Name", "Price", "Rating", "UpdatedAt" },
                values: new object[] { new Guid("60000000-0000-0000-0000-000000000001"), "Warm", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 12500, "https://example.com/presets/golden-hour.dng", "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=300&h=300&fit=crop", true, "Golden Hour", 49000m, 4.9m, null });

            migrationBuilder.InsertData(
                table: "style_tags",
                columns: new[] { "Id", "Color", "CreatedAt", "Emoji", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "#C44569", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "📸", "Vintage", null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "#FF6B9D", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "🎌", "Hàn Quốc", null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "#4A90E2", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "⚪", "Minimal", null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "#F5A623", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "🏙️", "Street", null }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "AvatarUrl", "CreatedAt", "Email", "FullName", "IsActive", "PasswordHash", "PhoneNumber", "Role", "UpdatedAt" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=96&h=96&fit=crop", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "grapher@picmate.vn", "Nguyễn Anh", true, "seeded-user-register-again-to-login", "0900000001", "Grapher", null });

            migrationBuilder.InsertData(
                table: "grapher_profiles",
                columns: new[] { "Id", "AverageRating", "Bio", "CccdBackImageUrl", "CccdFrontImageUrl", "CccdNumber", "CreatedAt", "District", "IsOnline", "IsVerified", "KycStatus", "Location", "ReviewCount", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), 4.9m, "Sinh viên đam mê chụp ảnh bằng điện thoại, chuyên ảnh lifestyle và vintage.", null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, true, "Approved", "TP.HCM", 234, null, new Guid("20000000-0000-0000-0000-000000000001") });

            migrationBuilder.InsertData(
                table: "grapher_portfolio_items",
                columns: new[] { "Id", "Caption", "CreatedAt", "DisplayOrder", "GrapherProfileId", "ImageUrl", "UpdatedAt" },
                values: new object[] { new Guid("50000000-0000-0000-0000-000000000001"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, new Guid("30000000-0000-0000-0000-000000000001"), "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=400&h=400&fit=crop", null });

            migrationBuilder.InsertData(
                table: "grapher_service_packages",
                columns: new[] { "Id", "CreatedAt", "Description", "DurationMinutes", "GrapherProfileId", "IsActive", "Name", "Price", "UpdatedAt" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Một giờ chụp bằng điện thoại, nhận ảnh trong ngày.", 60, new Guid("30000000-0000-0000-0000-000000000001"), true, "Hourly phone shoot", 150000m, null });

            migrationBuilder.InsertData(
                table: "grapher_style_tags",
                columns: new[] { "GrapherProfileId", "StyleTagId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerId_Status",
                table: "bookings",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_GrapherProfileId_ScheduledAt",
                table: "bookings",
                columns: new[] { "GrapherProfileId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_ServicePackageId",
                table: "bookings",
                column: "ServicePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_grapher_activity_areas_City_District",
                table: "grapher_activity_areas",
                columns: new[] { "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_grapher_activity_areas_GrapherProfileId",
                table: "grapher_activity_areas",
                column: "GrapherProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_grapher_portfolio_items_GrapherProfileId_DisplayOrder",
                table: "grapher_portfolio_items",
                columns: new[] { "GrapherProfileId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_grapher_profiles_CccdNumber",
                table: "grapher_profiles",
                column: "CccdNumber",
                unique: true,
                filter: "\"CccdNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_grapher_profiles_IsVerified_AverageRating",
                table: "grapher_profiles",
                columns: new[] { "IsVerified", "AverageRating" });

            migrationBuilder.CreateIndex(
                name: "IX_grapher_profiles_Location",
                table: "grapher_profiles",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_grapher_profiles_UserId",
                table: "grapher_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grapher_service_packages_GrapherProfileId_Price",
                table: "grapher_service_packages",
                columns: new[] { "GrapherProfileId", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_grapher_style_tags_StyleTagId",
                table: "grapher_style_tags",
                column: "StyleTagId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_BookingId",
                table: "payment_transactions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_ProviderTransactionId",
                table: "payment_transactions",
                column: "ProviderTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_TransactionCode",
                table: "payment_transactions",
                column: "TransactionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_presets_Category_Price",
                table: "presets",
                columns: new[] { "Category", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_BookingId",
                table: "reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_CustomerId",
                table: "reviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_GrapherProfileId_Rating",
                table: "reviews",
                columns: new[] { "GrapherProfileId", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_style_tags_Name",
                table: "style_tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grapher_activity_areas");

            migrationBuilder.DropTable(
                name: "grapher_portfolio_items");

            migrationBuilder.DropTable(
                name: "grapher_style_tags");

            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "presets");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "style_tags");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "grapher_service_packages");

            migrationBuilder.DropTable(
                name: "grapher_profiles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
