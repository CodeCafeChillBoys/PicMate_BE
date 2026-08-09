using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneGrapher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceKycWithApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grapher_profiles_CccdNumber",
                table: "grapher_profiles");

            migrationBuilder.DropColumn(
                name: "CccdBackImageUrl",
                table: "grapher_profiles");

            migrationBuilder.DropColumn(
                name: "CccdNumber",
                table: "grapher_profiles");

            migrationBuilder.RenameColumn(
                name: "CccdFrontImageUrl",
                table: "grapher_profiles",
                newName: "CvFileUrl");

            migrationBuilder.AddColumn<int>(
                name: "ExperienceYears",
                table: "grapher_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalLinks",
                table: "grapher_profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycRejectReason",
                table: "grapher_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialization",
                table: "grapher_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "grapher_profiles",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "ExperienceYears", "ExternalLinks", "KycRejectReason", "Specialization" },
                values: new object[] { null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperienceYears",
                table: "grapher_profiles");

            migrationBuilder.DropColumn(
                name: "ExternalLinks",
                table: "grapher_profiles");

            migrationBuilder.DropColumn(
                name: "KycRejectReason",
                table: "grapher_profiles");

            migrationBuilder.DropColumn(
                name: "Specialization",
                table: "grapher_profiles");

            migrationBuilder.RenameColumn(
                name: "CvFileUrl",
                table: "grapher_profiles",
                newName: "CccdFrontImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "CccdBackImageUrl",
                table: "grapher_profiles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdNumber",
                table: "grapher_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "grapher_profiles",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "CccdBackImageUrl", "CccdNumber" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_grapher_profiles_CccdNumber",
                table: "grapher_profiles",
                column: "CccdNumber",
                unique: true,
                filter: "\"CccdNumber\" IS NOT NULL");
        }
    }
}
