using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedAttachmentEntityAndOtherModelUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "LeaveDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecureUrl",
                table: "LeaveDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "LeaveDocuments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ETag",
                table: "LeaveDocuments");

            migrationBuilder.DropColumn(
                name: "SecureUrl",
                table: "LeaveDocuments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LeaveDocuments");
        }
    }
}
