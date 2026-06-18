using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synctool.Migrations
{
    /// <inheritdoc />
    public partial class UpdateManualCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPrice",
                table: "ManualCampaigns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTargetProduct",
                table: "ManualCampaignProducts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPrice",
                table: "ManualCampaigns");

            migrationBuilder.DropColumn(
                name: "IsTargetProduct",
                table: "ManualCampaignProducts");
        }
    }
}
