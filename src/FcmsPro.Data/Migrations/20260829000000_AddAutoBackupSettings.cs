using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FcmsPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoBackupSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoBackupOnCloseEnabled",
                table: "UiPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoBackupKeepCount",
                table: "UiPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoBackupOnCloseEnabled",
                table: "UiPreferences");

            migrationBuilder.DropColumn(
                name: "AutoBackupKeepCount",
                table: "UiPreferences");
        }
    }
}
