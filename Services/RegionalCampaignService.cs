using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Synctool.Services
{
    public class RegionalCampaignItem
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; } // Brüt İndirim (Örn: 6.000 TL)
        public decimal NetDiscountAmount => Math.Round(DiscountAmount * 0.85m, 2); // Net İndirim (%85 -> 5.100 TL)
        public string Note { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public static class RegionalCampaignService
    {
        private static readonly string _settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Synctool");

        private static readonly string _settingsFile = Path.Combine(_settingsDir, "regional_campaigns.json");

        private static Dictionary<string, RegionalCampaignItem> _cache = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
            _loaded = true;
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile);
                    var items = JsonSerializer.Deserialize<List<RegionalCampaignItem>>(json);
                    if (items != null)
                    {
                        _cache = items
                            .Where(x => !string.IsNullOrWhiteSpace(x.ProductCode))
                            .ToDictionary(x => x.ProductCode.Trim().ToUpperInvariant(), x => x, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                _cache = new Dictionary<string, RegionalCampaignItem>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);
                var list = _cache.Values.OrderBy(x => x.ProductCode).ToList();
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch
            {
                // Diske yazma hatası
            }
        }

        public static List<RegionalCampaignItem> GetAll()
        {
            EnsureLoaded();
            return _cache.Values.OrderBy(x => x.ProductCode).ToList();
        }

        public static RegionalCampaignItem? GetByProductCode(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return null;
            EnsureLoaded();
            string key = productCode.Trim().ToUpperInvariant();
            return _cache.TryGetValue(key, out var item) ? item : null;
        }

        public static void SaveOrUpdate(RegionalCampaignItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProductCode)) return;
            EnsureLoaded();
            string key = item.ProductCode.Trim().ToUpperInvariant();
            item.ProductCode = key;
            item.UpdatedAt = DateTime.Now;
            _cache[key] = item;
            Save();
        }

        public static bool Delete(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return false;
            EnsureLoaded();
            string key = productCode.Trim().ToUpperInvariant();
            if (_cache.Remove(key))
            {
                Save();
                return true;
            }
            return false;
        }

        public static string GetJsonFilePath() => _settingsFile;
    }
}
