using System;

namespace Synctool.Models
{
    public class CartItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public decimal SelectedPrice { get; set; }
        public decimal CardPurchasePrice { get; set; }
        public decimal CardMarkupPercent { get; set; }
        public bool IsParolu { get; set; }
        public string? ProductType { get; set; } // "KEA" or "BeyazEsya"
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
