using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaviaUp.Admin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationStatusSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operation_status_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InactivityRuleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InactivityThresholdMinutes = table.Column<int>(type: "integer", nullable: false),
                    InactivitySeverity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashRegisterRuleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CashRegisterSeverity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_status_settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "operation_status_settings",
                columns: new[]
                {
                    "Id",
                    "InactivityRuleEnabled",
                    "InactivityThresholdMinutes",
                    "InactivitySeverity",
                    "CashRegisterRuleEnabled",
                    "CashRegisterSeverity",
                    "UpdatedAt",
                    "UpdatedByAdminUserId"
                },
                values: new object[]
                {
                    new Guid("9d829759-a1ce-4e61-91da-973b1fe725bd"),
                    true,
                    120,
                    "CRITICAL",
                    true,
                    "WARNING",
                    DateTimeOffset.MinValue,
                    null!
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operation_status_settings");
        }
    }
}
