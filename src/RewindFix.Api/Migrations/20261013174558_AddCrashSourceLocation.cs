using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RewindFix.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCrashSourceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "Crashes",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "Crashes",
                type: "varchar(4096)",
                maxLength: 4096,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SourceLine",
                table: "Crashes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMethod",
                table: "Crashes",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "Crashes");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "Crashes");

            migrationBuilder.DropColumn(
                name: "SourceLine",
                table: "Crashes");

            migrationBuilder.DropColumn(
                name: "SourceMethod",
                table: "Crashes");
        }
    }
}
