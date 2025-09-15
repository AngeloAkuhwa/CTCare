using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BaseEntity_Updated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_Prefix_IsActive",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeavePolicies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeaveDocuments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeaveApprovalSteps");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ApiKeys");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "ApiKeys",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix_IsDeleted",
                table: "ApiKeys",
                columns: new[] { "Prefix", "IsDeleted" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_Prefix_IsDeleted",
                table: "ApiKeys");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Teams",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RefreshTokens",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeaveTypes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeaveRequests",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeavePolicies",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeaveDocuments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeaveApprovalSteps",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Employees",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Departments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AuditLogs",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "ApiKeys",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ApiKeys",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix_IsActive",
                table: "ApiKeys",
                columns: new[] { "Prefix", "IsActive" },
                unique: true);
        }
    }
}
