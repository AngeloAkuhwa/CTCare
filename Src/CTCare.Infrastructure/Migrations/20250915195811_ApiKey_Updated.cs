using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiKey_Updated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ApiKeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ApiKeys",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
