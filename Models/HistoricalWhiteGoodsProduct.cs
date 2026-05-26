using System;

namespace Synctool.Models
{
    public class HistoricalWhiteGoodsProduct
    {
        public int Id { get; set; }

        // --- Dönem Bilgisi ---
        public int PeriodMonth { get; set; }
        public int PeriodYear { get; set; }
        public DateTime ArchiveDate { get; set; } = DateTime.Now;

        // --- Ürün Kimliği ---
        public string ExcelFileType      { get; set; } = string.Empty; // "Ankastre", "Sogutucu" vb.
        public string ProductCode        { get; set; } = string.Empty;
        public string ProductName        { get; set; } = string.Empty;
        public string Description        { get; set; } = string.Empty;
        public string EnergyClass        { get; set; } = string.Empty;

        // --- Peşin / Toptan Fiyatlar ---
        public decimal? CashPrice         { get; set; }
        public decimal? WholesalePrice30  { get; set; }
        public decimal? WholesalePrice60  { get; set; }
        public decimal? WholesalePrice90  { get; set; }
        public decimal? WholesalePrice120 { get; set; }

        // --- Taksit Fiyatları ---
        public decimal? Installment2Down  { get; set; }
        public decimal? Installment2Total { get; set; }
        public decimal? Installment4Down  { get; set; }
        public decimal? Installment4Total { get; set; }
        public decimal? Installment8Down  { get; set; }
        public decimal? Installment8Total { get; set; }

        // --- Kampanya / Promo Fiyatları ---
        public decimal? PromoCashPrice    { get; set; }
        public decimal? PromoInstall1x2   { get; set; }
        public decimal? PromoInstall1x4   { get; set; }
        public decimal? PromoInstall1x8   { get; set; }

        // --- İlişki ---
        public int UploadedFileId { get; set; }
        public UploadedFile? UploadedFile { get; set; }
    }
}
