using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FcmsPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        private static readonly string[] Tables =
        {
            "Clients", "Commissions", "Payments", "Expenses", "Quotes", "Invoices"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<bool>(
                    name: "IsDeleted",
                    table: table,
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: false);

                migrationBuilder.AddColumn<System.DateTimeOffset>(
                    name: "DeletedAt",
                    table: table,
                    type: "TEXT",
                    nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.DropColumn(name: "IsDeleted", table: table);
                migrationBuilder.DropColumn(name: "DeletedAt", table: table);
            }
        }
    }
}
