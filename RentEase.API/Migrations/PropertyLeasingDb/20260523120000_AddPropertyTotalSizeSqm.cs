using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropertyLeasing.API.Data;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    [DbContext(typeof(PropertyLeasingDbContext))]
    [Migration("20260523120000_AddPropertyTotalSizeSqm")]
    /// <inheritdoc />
    public partial class AddPropertyTotalSizeSqm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TotalSizeSqm",
                table: "Property",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalSizeSqm",
                table: "Property");
        }
    }
}
