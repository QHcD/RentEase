using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddMaintenanceStaffTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "User");

            migrationBuilder.DropColumn(
                name: "SkillProfile",
                table: "User");

            migrationBuilder.CreateTable(
                name: "MaintenanceStaff",
                columns: table => new
                {
                    StaffID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    SkillProfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AvailabilityStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceStaff", x => x.StaffID);
                    table.ForeignKey(
                        name: "FK_MaintenanceStaff_User",
                        column: x => x.UserID,
                        principalTable: "User",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceStaff_UserID",
                table: "MaintenanceStaff",
                column: "UserID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceStaff");

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityStatus",
                table: "User",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillProfile",
                table: "User",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
