using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMobileAssignment.Migrations
{
    /// <inheritdoc />
    public partial class addIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Flag",
                table: "Attendances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Attendances",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Flag",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Attendances");
        }
    }
}
