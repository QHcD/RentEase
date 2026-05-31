using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropertyLeasing.API.Data;

#nullable disable

namespace PropertyLeasing.API.Migrations.PropertyLeasingDb
{
    [DbContext(typeof(PropertyLeasingDbContext))]
    [Migration("20260527120000_AddAmenityLinkTables")]
    public partial class AddAmenityLinkTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Amenity",
                columns: table => new
                {
                    AmenityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenity", x => x.AmenityID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Amenity_Name",
                table: "Amenity",
                column: "Name",
                unique: true);

            migrationBuilder.CreateTable(
                name: "PropertyAmenities",
                columns: table => new
                {
                    PropertyID = table.Column<int>(type: "int", nullable: false),
                    AmenityID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAmenities", x => new { x.PropertyID, x.AmenityID });
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Amenity",
                        column: x => x.AmenityID,
                        principalTable: "Amenity",
                        principalColumn: "AmenityID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Property",
                        column: x => x.PropertyID,
                        principalTable: "Property",
                        principalColumn: "PropertyID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitAmenities",
                columns: table => new
                {
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    AmenityID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitAmenities", x => new { x.UnitID, x.AmenityID });
                    table.ForeignKey(
                        name: "FK_UnitAmenities_Amenity",
                        column: x => x.AmenityID,
                        principalTable: "Amenity",
                        principalColumn: "AmenityID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnitAmenities_Unit",
                        column: x => x.UnitID,
                        principalTable: "Unit",
                        principalColumn: "UnitID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAmenities_AmenityID",
                table: "PropertyAmenities",
                column: "AmenityID");

            migrationBuilder.CreateIndex(
                name: "IX_UnitAmenities_AmenityID",
                table: "UnitAmenities",
                column: "AmenityID");

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('Property', 'Amenities') IS NOT NULL
                BEGIN
                    INSERT INTO [Amenity] ([Name])
                    SELECT DISTINCT LTRIM(RTRIM(value)) AS [Name]
                    FROM [Property]
                    CROSS APPLY STRING_SPLIT([Amenities], ',')
                    WHERE [Amenities] IS NOT NULL
                      AND LTRIM(RTRIM(value)) <> ''
                      AND NOT EXISTS (
                          SELECT 1 FROM [Amenity] a WHERE a.[Name] = LTRIM(RTRIM(value))
                      );

                    INSERT INTO [PropertyAmenities] ([PropertyID], [AmenityID])
                    SELECT DISTINCT p.[PropertyID], a.[AmenityID]
                    FROM [Property] p
                    CROSS APPLY STRING_SPLIT(p.[Amenities], ',') s
                    INNER JOIN [Amenity] a ON a.[Name] = LTRIM(RTRIM(s.value))
                    WHERE p.[Amenities] IS NOT NULL
                      AND LTRIM(RTRIM(s.value)) <> '';

                    ALTER TABLE [Property] DROP COLUMN [Amenities];
                END
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('Unit', 'Amenities') IS NOT NULL
                BEGIN
                    INSERT INTO [Amenity] ([Name])
                    SELECT DISTINCT LTRIM(RTRIM(value)) AS [Name]
                    FROM [Unit]
                    CROSS APPLY STRING_SPLIT([Amenities], ',')
                    WHERE [Amenities] IS NOT NULL
                      AND LTRIM(RTRIM(value)) <> ''
                      AND NOT EXISTS (
                          SELECT 1 FROM [Amenity] a WHERE a.[Name] = LTRIM(RTRIM(value))
                      );

                    INSERT INTO [UnitAmenities] ([UnitID], [AmenityID])
                    SELECT DISTINCT u.[UnitID], a.[AmenityID]
                    FROM [Unit] u
                    CROSS APPLY STRING_SPLIT(u.[Amenities], ',') s
                    INNER JOIN [Amenity] a ON a.[Name] = LTRIM(RTRIM(s.value))
                    WHERE u.[Amenities] IS NOT NULL
                      AND LTRIM(RTRIM(s.value)) <> '';

                    ALTER TABLE [Unit] DROP COLUMN [Amenities];
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Unit",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Property",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.DropTable(name: "UnitAmenities");
            migrationBuilder.DropTable(name: "PropertyAmenities");
            migrationBuilder.DropTable(name: "Amenity");
        }
    }
}
