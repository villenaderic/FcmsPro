using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FcmsPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAutomationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoInvoiceOnDelivery",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceDueDays",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoInvoiceOnDelivery",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceDueDays",
                table: "AppSettings");
        }
    }
}
