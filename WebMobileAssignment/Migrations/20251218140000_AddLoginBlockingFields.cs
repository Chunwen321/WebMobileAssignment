using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMobileAssignment.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginBlockingFields : Migration
    {
        /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
        {
       migrationBuilder.AddColumn<int>(
   name: "FailedLoginAttempts",
        table: "Users",
     type: "int",
       nullable: false,
   defaultValue: 0);

 migrationBuilder.AddColumn<DateTime>(
     name: "LastFailedLogin",
     table: "Users",
                type: "datetime2",
         nullable: true);

     migrationBuilder.AddColumn<DateTime>(
           name: "LockoutEnd",
    table: "Users",
     type: "datetime2",
        nullable: true);
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
    migrationBuilder.DropColumn(
   name: "FailedLoginAttempts",
     table: "Users");

          migrationBuilder.DropColumn(
         name: "LastFailedLogin",
    table: "Users");

            migrationBuilder.DropColumn(
       name: "LockoutEnd",
    table: "Users");
        }
    }
}
