using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddPaymentPlanTypeToLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentPlanType",
                table: "Lease",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentPlanType",
                table: "Lease");
        }
    }
}
