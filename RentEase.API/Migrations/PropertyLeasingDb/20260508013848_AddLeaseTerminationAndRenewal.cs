using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    /// <inheritdoc />
    public partial class AddLeaseTerminationAndRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentLeaseID",
                table: "LeaseApplication",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewLeaseApplicationID",
                table: "Lease",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TerminationID",
                table: "Lease",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Termination",
                columns: table => new
                {
                    TerminationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminationDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Termination", x => x.TerminationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseApplication_ParentLeaseID",
                table: "LeaseApplication",
                column: "ParentLeaseID");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_RenewLeaseApplicationID",
                table: "Lease",
                column: "RenewLeaseApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_TerminationID",
                table: "Lease",
                column: "TerminationID",
                unique: true,
                filter: "[TerminationID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Lease_RenewLeaseApplication",
                table: "Lease",
                column: "RenewLeaseApplicationID",
                principalTable: "LeaseApplication",
                principalColumn: "ApplicationID");

            migrationBuilder.AddForeignKey(
                name: "FK_Lease_Termination",
                table: "Lease",
                column: "TerminationID",
                principalTable: "Termination",
                principalColumn: "TerminationId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaseApplication_ParentLease",
                table: "LeaseApplication",
                column: "ParentLeaseID",
                principalTable: "Lease",
                principalColumn: "LeaseID");

            // Data migration: rename obsolete 'Expired' status → 'Terminated'
            migrationBuilder.Sql("UPDATE [Lease] SET [Status] = 'Terminated' WHERE [Status] = 'Expired';");
            migrationBuilder.Sql("UPDATE [LeaseLog] SET [Status] = 'Terminated' WHERE [Status] = 'Expired';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lease_RenewLeaseApplication",
                table: "Lease");

            migrationBuilder.DropForeignKey(
                name: "FK_Lease_Termination",
                table: "Lease");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaseApplication_ParentLease",
                table: "LeaseApplication");

            migrationBuilder.DropTable(
                name: "Termination");

            migrationBuilder.DropIndex(
                name: "IX_LeaseApplication_ParentLeaseID",
                table: "LeaseApplication");

            migrationBuilder.DropIndex(
                name: "IX_Lease_RenewLeaseApplicationID",
                table: "Lease");

            migrationBuilder.DropIndex(
                name: "IX_Lease_TerminationID",
                table: "Lease");

            migrationBuilder.DropColumn(
                name: "ParentLeaseID",
                table: "LeaseApplication");

            migrationBuilder.DropColumn(
                name: "RenewLeaseApplicationID",
                table: "Lease");

            migrationBuilder.DropColumn(
                name: "TerminationID",
                table: "Lease");
        }
    }
}
