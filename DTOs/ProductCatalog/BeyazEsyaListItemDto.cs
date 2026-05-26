using System;

namespace Synctool.DTOs
{
    public class BeyazEsyaListItemDto
    {
        public int Id { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string SourceTable { get; set; } = string.Empty;
        public string PricePPSource { get; set; } = string.Empty;
        public decimal PricePP { get; set; }
        public decimal PriceConversion { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal CardMarkupPercent { get; set; }
        public decimal CardPurchasePrice { get; set; }
        public string CampaingDate { get; set; } = string.Empty;
        public string ExcelFileType { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string ManualCampaignText { get; set; } = string.Empty;

        // Individual valor prices from source product tables
        public decimal CashPrice { get; set; }
        public decimal WholesalePrice30 { get; set; }
        public decimal WholesalePrice60 { get; set; }
        public decimal WholesalePrice90 { get; set; }
        public decimal WholesalePrice120 { get; set; }
    }
}
