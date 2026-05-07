using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddGracePeriodAndLateFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GracePeriodDays",
                table: "Property",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeePercent",
                table: "Property",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFee",
                table: "PaymentRecord",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodDays",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "LateFeePercent",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "LateFee",
                table: "PaymentRecord");
        }
    }
}
