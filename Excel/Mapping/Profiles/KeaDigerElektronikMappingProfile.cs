using Synctool.Models;

namespace Synctool.Excel.Mapping.Profiles
{
    /// <summary>
    /// KEA Diğer Elektronik Excel dosyası kolon mapping profili.
    /// </summary>
    public static class KeaDigerElektronikMappingProfile
    {
        public static ColumnMappingProfile Get() => new()
        {
            FileTypeName = "Diğer Elektronik",
            HeaderRow    = 1,
            DataStartRow = 2,

            FieldToColumnIndex = new System.Collections.Generic.Dictionary<string, int>
            {
                { nameof(KeaProduct.ProductCode),       1  },  // A -> SKU
                { nameof(KeaProduct.ProductName),       2  },  // B -> Ürün Adı
                
                // PP (F+30) kolonu C sütununda (3. kolon).
                { nameof(KeaProduct.WholesalePrice30),  3  }   // C -> PP (F+30)
            }
        };
    }
}
