using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Synctool.Data;
using Synctool.Services;

namespace Synctool.Views
{
    public partial class RegionalCampaignView : UserControl
    {
        public class ProductSearchDto
        {
            public string ProductCode { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
        }

        private List<ProductSearchDto> _allProducts = new();
        private List<RegionalCampaignItem> _loadedCampaigns = new();

        public RegionalCampaignView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadProductCatalogAsync();
            RefreshGrid();
        }

        private async Task LoadProductCatalogAsync()
        {
            try
            {
                var products = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var list = new List<ProductSearchDto>();

                    var kea = db.KeaProducts
                        .Where(k => !string.IsNullOrEmpty(k.ProductCode))
                        .Select(k => new ProductSearchDto { ProductCode = k.ProductCode, ProductName = k.ProductName ?? k.Description })
                        .ToList();

                    var wg = db.WhiteGoodsProducts
                        .Where(w => !string.IsNullOrEmpty(w.ProductCode))
                        .Select(w => new ProductSearchDto { ProductCode = w.ProductCode, ProductName = w.ProductName ?? w.Description })
                        .ToList();

                    list.AddRange(kea);
                    list.AddRange(wg);

                    return list
                        .GroupBy(p => p.ProductCode.Trim().ToUpperInvariant())
                        .Select(g => g.First())
                        .OrderBy(p => p.ProductCode)
                        .ToList();
                });

                _allProducts = products;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ürün kataloğu yüklenirken hata: {ex.Message}");
            }
        }

        private void RefreshGrid()
        {
            _loadedCampaigns = RegionalCampaignService.GetAll();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filter = TxtFilterList?.Text?.Trim().ToLower() ?? "";
            var items = string.IsNullOrEmpty(filter)
                ? _loadedCampaigns
                : _loadedCampaigns.Where(x => x.ProductCode.ToLower().Contains(filter) || x.ProductName.ToLower().Contains(filter) || x.Note.ToLower().Contains(filter)).ToList();

            GridRegionalCampaigns.ItemsSource = null;
            GridRegionalCampaigns.ItemsSource = items;
            TxtTotalCount.Text = $"{items.Count} Kayıt";
        }

        private void TxtSearchProduct_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearchProduct.Text?.Trim() ?? "";
            if (query.Length < 2)
            {
                BorderSearchResults.Visibility = Visibility.Collapsed;
                LstSearchResults.ItemsSource = null;
                return;
            }

            string upper = query.ToUpperInvariant();
            var matches = _allProducts
                .Where(p => p.ProductCode.ToUpperInvariant().Contains(upper) || p.ProductName.ToUpperInvariant().Contains(upper))
                .Take(15)
                .ToList();

            if (matches.Any())
            {
                LstSearchResults.ItemsSource = matches;
                BorderSearchResults.Visibility = Visibility.Visible;
            }
            else
            {
                BorderSearchResults.Visibility = Visibility.Collapsed;
                LstSearchResults.ItemsSource = null;
            }
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSearchResults.SelectedItem is ProductSearchDto item)
            {
                TxtProductCode.Text = item.ProductCode;
                TxtProductName.Text = item.ProductName;
                BorderSearchResults.Visibility = Visibility.Collapsed;

                // Var olan kampanyayı form alanına getir
                var existing = RegionalCampaignService.GetByProductCode(item.ProductCode);
                if (existing != null)
                {
                    TxtDiscountAmount.Text = existing.DiscountAmount.ToString("N0");
                    TxtNote.Text = existing.Note;
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtProductCode.Text?.Trim() ?? "";
            string name = TxtProductName.Text?.Trim() ?? "";
            string discountStr = TxtDiscountAmount.Text?.Trim().Replace(".", "").Replace(",", ".") ?? "";

            if (string.IsNullOrWhiteSpace(code))
            {
                await ModernDialogService.ShowAsync("Uyarı", "Lütfen bir ürün kodu girin veya listeden seçin.", ModernDialogType.Warning);
                return;
            }

            if (!decimal.TryParse(discountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal discount) || discount < 0)
            {
                await ModernDialogService.ShowAsync("Uyarı", "Lütfen geçerli bir bölgesel indirim tutarı girin.", ModernDialogType.Warning);
                return;
            }

            var item = new RegionalCampaignItem
            {
                ProductCode = code,
                ProductName = name,
                DiscountAmount = discount,
                Note = TxtNote.Text?.Trim() ?? "",
                UpdatedAt = DateTime.Now
            };

            RegionalCampaignService.SaveOrUpdate(item);
            SyncToDatabase(code);
            ClearForm();
            RefreshGrid();

            await ModernDialogService.ShowAsync("Başarılı", $"{code} kodlu ürün için {discount:N2} ₺ bölgesel indirim kaydedildi.", ModernDialogType.Success);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtSearchProduct.Text = "";
            TxtProductCode.Text = "";
            TxtProductName.Text = "";
            TxtDiscountAmount.Text = "";
            TxtNote.Text = "";
            BorderSearchResults.Visibility = Visibility.Collapsed;
        }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RegionalCampaignItem item)
            {
                TxtProductCode.Text = item.ProductCode;
                TxtProductName.Text = item.ProductName;
                TxtDiscountAmount.Text = item.DiscountAmount.ToString("N0");
                TxtNote.Text = item.Note;
            }
        }

        private async void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RegionalCampaignItem item)
            {
                bool confirm = await ModernDialogService.ShowAsync("Silme Onayı", $"{item.ProductCode} kodlu bölgesel kampanya kaydını silmek istediğinize emin misiniz?", ModernDialogType.Question);
                if (confirm)
                {
                    RegionalCampaignService.Delete(item.ProductCode);
                    SyncToDatabase(item.ProductCode);
                    RefreshGrid();
                }
            }
        }

        private static void SyncToDatabase(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return;
            try
            {
                string key = productCode.Trim().ToUpperInvariant();
                var regionalCamp = RegionalCampaignService.GetByProductCode(key);

                using var db = new AppDbContext();
                var existingCalcs = db.CostCalculations
                    .Where(c => c.ProductCode != null && c.ProductCode.Trim().ToUpper() == key)
                    .ToList();

                if (existingCalcs.Any())
                {
                    foreach (var calc in existingCalcs)
                    {
                        if (regionalCamp != null)
                        {
                            calc.PriceConversion = Math.Round(regionalCamp.DiscountAmount * 0.85m, 2); // Brüt %85 -> Net İndirim (Örn: 6.000 TL -> 5.100 TL)
                            calc.PurchasePrice = Math.Max(0, calc.PricePP - calc.PriceConversion);
                            calc.CardPurchasePrice = Math.Round(calc.PurchasePrice * (1 + calc.CardMarkupPercent / 100m), 2);
                            calc.CampaingDate = "Bölgesel Kampanya";
                        }
                        else
                        {
                            var oliz = db.OlizCampaigns.FirstOrDefault(o => o.ProductCode != null && o.ProductCode.Trim().ToUpper() == key);
                            if (oliz != null)
                            {
                                calc.PriceConversion = oliz.DiscountNetAmount;
                                calc.PurchasePrice = Math.Max(0, calc.PricePP - calc.PriceConversion);
                                calc.CardPurchasePrice = Math.Round(calc.PurchasePrice * (1 + calc.CardMarkupPercent / 100m), 2);
                                calc.CampaingDate = $"{oliz.CampaignStartDate} - {oliz.CampaignEndDate}";
                            }
                            else
                            {
                                calc.PriceConversion = 0m;
                                calc.PurchasePrice = calc.PricePP;
                                calc.CardPurchasePrice = Math.Round(calc.PurchasePrice * (1 + calc.CardMarkupPercent / 100m), 2);
                                calc.CampaingDate = string.Empty;
                            }
                        }
                    }
                    db.SaveChanges();
                }

                BeyazEsyaView.ClearCache();
                KeaView.ClearCache();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DB senkronizasyon hatası: {ex.Message}");
            }
        }

        private void TxtFilterList_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void BtnOpenJsonFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string jsonPath = RegionalCampaignService.GetJsonFilePath();
                if (File.Exists(jsonPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{jsonPath}\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(jsonPath) ?? "";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
            catch (Exception ex)
            {
                _ = ModernDialogService.ShowAsync("Hata", $"Dosya konumu açılamadı: {ex.Message}", ModernDialogType.Error);
            }
        }
    }
}
