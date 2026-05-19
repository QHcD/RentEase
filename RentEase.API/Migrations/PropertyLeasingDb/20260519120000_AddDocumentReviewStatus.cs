using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddDocumentReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Document",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Document",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Submitted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Document");
        }
    }
}
