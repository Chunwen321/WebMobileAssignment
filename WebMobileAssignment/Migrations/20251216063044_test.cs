using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMobileAssignment.Migrations
{
    /// <inheritdoc />
    public partial class test : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfilePicture",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PinCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    ClassId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByTeacherId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SessionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Teachers_CreatedByTeacherId",
                        column: x => x.CreatedByTeacherId,
                        principalTable: "Teachers",
                        principalColumn: "TeacherId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_ClassId",
                table: "AttendanceSessions",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CreatedByTeacherId",
                table: "AttendanceSessions",
                column: "CreatedByTeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                table: "Users");
        }
    }
}
