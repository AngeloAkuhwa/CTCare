using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedConcurrencyTokensUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LeaveRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LeaveRequests",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
