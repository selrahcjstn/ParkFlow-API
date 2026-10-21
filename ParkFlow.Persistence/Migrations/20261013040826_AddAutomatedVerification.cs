using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stop without changing existing data if legacy plate duplicates need remediation.
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Vehicles_NormalizedPlate\" ON \"Vehicles\" (upper(replace(replace(\"PlateNumber\", '-', ''), ' ', '')));");
            migrationBuilder.AddColumn<int>(
                name: "ScheduleVerificationStatus",
                table: "CorSubmissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerificationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerificationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountAutomaticApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CorAutomaticApproval = table.Column<bool>(type: "boolean", nullable: false),
                    VehicleAutomaticApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduleAutomaticApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Semester = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaxScheduleHoursPerDay = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationPolicies", x => x.Id);
                    table.CheckConstraint("CK_VerificationPolicy_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "VerificationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OcrOutcome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtractedFieldsJson = table.Column<string>(type: "jsonb", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationRecords_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "VerificationPolicies",
                columns: new[] { "Id", "AcademicYear", "AccountAutomaticApproval", "CorAutomaticApproval", "MaxScheduleHoursPerDay", "ScheduleAutomaticApproval", "Semester", "UpdatedAt", "UpdatedBy", "VehicleAutomaticApproval", "Version" },
                values: new object[] { 1, "", false, false, 8, false, "", null, null, false, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationAudits_VerificationRecordId_CreatedAt",
                table: "VerificationAudits",
                columns: new[] { "VerificationRecordId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRecords_Status_IsCurrent_NextAttemptAt",
                table: "VerificationRecords",
                columns: new[] { "Status", "IsCurrent", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRecords_Type_SubjectId",
                table: "VerificationRecords",
                columns: new[] { "Type", "SubjectId" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRecords_UserAccountId",
                table: "VerificationRecords",
                column: "UserAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Vehicles_NormalizedPlate\";");
            migrationBuilder.DropTable(
                name: "VerificationAudits");

            migrationBuilder.DropTable(
                name: "VerificationPolicies");

            migrationBuilder.DropTable(
                name: "VerificationRecords");

            migrationBuilder.DropColumn(
                name: "ScheduleVerificationStatus",
                table: "CorSubmissions");
        }
    }
}
