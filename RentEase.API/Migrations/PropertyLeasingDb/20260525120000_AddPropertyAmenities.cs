using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropertyLeasing.API.Data;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    [DbContext(typeof(PropertyLeasingDbContext))]
    [Migration("20260525120000_AddPropertyAmenities")]
    /// <inheritdoc />
    public partial class AddPropertyAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Property",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Property");
        }
    }
}
