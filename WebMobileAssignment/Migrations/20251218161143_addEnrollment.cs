using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMobileAssignment.Migrations
{
    /// <inheritdoc />
    public partial class addEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Enrollments",
                table: "Enrollments");

            migrationBuilder.AddColumn<string>(
                name: "EnrollmentId",
                table: "Enrollments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnenrolledDate",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Enrollments",
                table: "Enrollments",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_ClassId",
                table: "Enrollments",
                columns: new[] { "StudentId", "ClassId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Enrollments",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_ClassId",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "EnrollmentId",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "UnenrolledDate",
                table: "Enrollments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Enrollments",
                table: "Enrollments",
                columns: new[] { "StudentId", "ClassId" });
        }
    }
}
