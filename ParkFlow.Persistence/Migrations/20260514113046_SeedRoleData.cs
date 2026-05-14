using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "UserAccounts",
                columns: new[] { "Id", "CreatedAt", "Email", "PasswordHash", "PasswordLastUpdatedAt", "PasswordResetTokenExpiresAt", "PasswordResetTokenHash", "PhoneNumber", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "admin@parkflow.local", "seed-password-hash-admin", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "09170000001", 2, null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "guard@parkflow.local", "seed-password-hash-guard", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "09170000002", 2, null },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "personnel@parkflow.local", "seed-password-hash-personnel", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "09170000003", 2, null },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "student@parkflow.local", "seed-password-hash-student", new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "09170000004", 2, null }
                });

            migrationBuilder.InsertData(
                table: "UserProfiles",
                columns: new[] { "Id", "CreatedAt", "FirstName", "IdCardNumber", "LastName", "ProfilePictureUrl", "UpdatedAt", "UserAccountId" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Admin", "ADMIN-0001", "User", null, null, new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Guard", "GUARD-0001", "User", null, null, new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Personnel", "PERSONNEL-0001", "User", null, null, new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Student", "STUDENT-0001", "User", null, null, new Guid("44444444-4444-4444-4444-444444444444") }
                });

            migrationBuilder.InsertData(
                table: "Admins",
                columns: new[] { "UserProfileId", "RoleLevel" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 0 });

            migrationBuilder.InsertData(
                table: "Guards",
                columns: new[] { "UserProfileId", "AssignedGate" },
                values: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1 });

            migrationBuilder.InsertData(
                table: "Personnel",
                columns: new[] { "UserProfileId", "Department", "IdCardNumber" },
                values: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Administration", "PERSONNEL-0001" });

            migrationBuilder.InsertData(
                table: "Students",
                columns: new[] { "UserProfileId", "Course", "Section", "StudentNumber", "YearLevel" },
                values: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "BSIT", "A", "STUDENT-0001", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Admins",
                keyColumn: "UserProfileId",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "Guards",
                keyColumn: "UserProfileId",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "Personnel",
                keyColumn: "UserProfileId",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "Students",
                keyColumn: "UserProfileId",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"));

            migrationBuilder.DeleteData(
                table: "UserProfiles",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "UserProfiles",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "UserProfiles",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "UserProfiles",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"));

            migrationBuilder.DeleteData(
                table: "UserAccounts",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "UserAccounts",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "UserAccounts",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "UserAccounts",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));
        }
    }
}
