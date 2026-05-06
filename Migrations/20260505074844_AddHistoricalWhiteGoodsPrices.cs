using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArcelikExcelApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalWhiteGoodsPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodMonth",
                table: "WhiteGoodsProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PeriodYear",
                table: "WhiteGoodsProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HistoricalWhiteGoodsProducts",
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
                    EnergyClass = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CashPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice30 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice60 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice90 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    WholesalePrice120 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment2Down = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment2Total = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment4Down = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment4Total = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment8Down = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Installment8Total = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoCashPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x2 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x4 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PromoInstall1x8 = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    UploadedFileId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalWhiteGoodsProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalWhiteGoodsProducts_UploadedFiles_UploadedFileId",
                        column: x => x.UploadedFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalWhiteGoodsProducts_UploadedFileId",
                table: "HistoricalWhiteGoodsProducts",
                column: "UploadedFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalWhiteGoodsProducts");

            migrationBuilder.DropColumn(
                name: "PeriodMonth",
                table: "WhiteGoodsProducts");

            migrationBuilder.DropColumn(
                name: "PeriodYear",
                table: "WhiteGoodsProducts");
        }
    }
}
