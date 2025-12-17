using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMobileAssignment.Migrations
{
    /// <inheritdoc />
    public partial class updateLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApplications_Users_ReviewedBy",
                table: "LeaveApplications");

            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.DropTable(
                name: "LeaveDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LeaveApplications",
                table: "LeaveApplications");

            migrationBuilder.DropIndex(
                name: "IX_LeaveApplications_ReviewedBy",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "LeaveApplicationId",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ApplicationDate",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "EmergencyContact",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "EmergencyPhone",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "IsNotified",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "LeaveType",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ProofDocument",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ReviewerRemarks",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "LeaveApplications");

            migrationBuilder.RenameColumn(
                name: "UserType",
                table: "LeaveApplications",
                newName: "LeaveId");

            migrationBuilder.AddColumn<string>(
                name: "DocumentPaths",
                table: "LeaveApplications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveApplications",
                table: "LeaveApplications",
                column: "LeaveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeaveApplications",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "DocumentPaths",
                table: "LeaveApplications");

            migrationBuilder.RenameColumn(
                name: "LeaveId",
                table: "LeaveApplications",
                newName: "UserType");

            migrationBuilder.AddColumn<string>(
                name: "LeaveApplicationId",
                table: "LeaveApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApplicationDate",
                table: "LeaveApplications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContact",
                table: "LeaveApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyPhone",
                table: "LeaveApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNotified",
                table: "LeaveApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LeaveType",
                table: "LeaveApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProofDocument",
                table: "LeaveApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "LeaveApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "LeaveApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerRemarks",
                table: "LeaveApplications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "LeaveApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaveApplications",
                table: "LeaveApplications",
                column: "LeaveApplicationId");

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    LeaveBalanceId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemainingLeave = table.Column<int>(type: "int", nullable: false),
                    TotalLeave = table.Column<int>(type: "int", nullable: false),
                    UsedLeave = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.LeaveBalanceId);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LeaveApplicationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_LeaveDocuments_LeaveApplications_LeaveApplicationId",
                        column: x => x.LeaveApplicationId,
                        principalTable: "LeaveApplications",
                        principalColumn: "LeaveApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_ReviewedBy",
                table: "LeaveApplications",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_UserId",
                table: "LeaveBalances",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveDocuments_LeaveApplicationId",
                table: "LeaveDocuments",
                column: "LeaveApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApplications_Users_ReviewedBy",
                table: "LeaveApplications",
                column: "ReviewedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
