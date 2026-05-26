using Synctool.Models;

namespace Synctool.Excel.Mapping.Profiles
{
    /// <summary>
    /// KEA Cep Telefonu Excel dosyası kolon mapping profili.
    /// </summary>
    public static class KeaCepTelefonuMappingProfile
    {
        public static ColumnMappingProfile Get() => new()
        {
            FileTypeName = "Cep Telefonu",
            HeaderRow    = 1,
            DataStartRow = 2,

            FieldToColumnIndex = new System.Collections.Generic.Dictionary<string, int>
            {
                { nameof(KeaProduct.Description),       1  },  // A -> MARKA
                { nameof(KeaProduct.ProductCode),       2  },  // B -> SKU
                { nameof(KeaProduct.ProductName),       3  },  // C -> ÜRÜN ADI
                
                // Toptan (F+30) kolonu D sütununda (4. kolon).
                { nameof(KeaProduct.WholesalePrice30),  4  }   // D -> Toptan
            }
        };
    }
}
