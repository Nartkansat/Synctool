using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synctool.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalKeaPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodMonth",
                table: "KeaProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PeriodYear",
                table: "KeaProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HistoricalKeaProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    ArchiveDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExcelFileType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CashPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice30 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice60 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice90 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice120 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoCashPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x2 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x4 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x8 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    UploadedFileId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalKeaProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalKeaProducts_UploadedFiles_UploadedFileId",
                        column: x => x.UploadedFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalKeaProducts_UploadedFileId",
                table: "HistoricalKeaProducts",
                column: "UploadedFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalKeaProducts");

            migrationBuilder.DropColumn(
                name: "PeriodMonth",
                table: "KeaProducts");

            migrationBuilder.DropColumn(
                name: "PeriodYear",
                table: "KeaProducts");
        }
    }
}
