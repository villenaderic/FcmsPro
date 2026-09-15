using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FcmsPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurFrequency",
                table: "Expenses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<System.DateOnly>(
                name: "NextOccurrence",
                table: "Expenses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_NextOccurrence",
                table: "Expenses",
                column: "NextOccurrence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_NextOccurrence",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurFrequency",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "NextOccurrence",
                table: "Expenses");
        }
    }
}
