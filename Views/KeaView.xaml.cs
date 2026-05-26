using Synctool.Data;
using Synctool.Models;
using Synctool.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using Synctool.Models;
using Synctool.Services;
using Microsoft.EntityFrameworkCore;

namespace Synctool.Views
{
    public partial class KeaView : UserControl
    {
        // Statik ön bellek — view kapatılıp açılsa bile veri kaybolmaz, anlık çalışır
        private static List<CalculationDisplayItem> _cache = new();
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(10);

        public static void ClearCache()
        {
            _cache.Clear();
            _cacheTime = DateTime.MinValue;
        }

        private List<CalculationDisplayItem> _filteredData = new();
        private string _currentSearchQuery = string.Empty;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalCount = 0;

        public class CalculationDisplayItem : CostCalculation
        {
            public string ExcelFileType { get; set; } = string.Empty;
            public string ManualCampaignText { get; set; } = string.Empty;
            public bool HasManualCampaign => !string.IsNullOrEmpty(ManualCampaignText);

            public decimal CashPrice { get; set; }
            public decimal WholesalePrice30 { get; set; }
            public decimal WholesalePrice60 { get; set; }
            public decimal WholesalePrice90 { get; set; }
            public decimal WholesalePrice120 { get; set; }
            public decimal OriginalPricePP { get; set; }
            public decimal OriginalPurchasePrice { get; set; }
            public string ActiveValorText { get; set; } = string.Empty;
            public decimal DiscountAmount { get; set; }
        }

        public KeaView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = InitAsync();
            this.Focus();
        }

        private async Task InitAsync()
        {
            await EnsureCacheAsync();
            if (_cache.Count > 0)
                ColCardPrice.Header = $"Kart Fiyat\u0131 (%{Convert.ToInt32(_cache[0].CardMarkupPercent)})";
            ApplyValorFilter();
            FilterAndDisplay(_currentSearchQuery);

            // Initialize valor rows once
            _valorRows = ValorSettingsService.KeaCategories
                .Select(cat =>
                {
                    var savedValor = ValorSettingsService.GetValor(cat);
                    return new ValorSettingRow
                    {
                        CategoryName     = cat,
                        OriginalValorKey = savedValor,
                        SelectedValorKey = savedValor
                    };
                })
                .ToList();
            IcValorSettings.ItemsSource = _valorRows;
        }

        private async Task EnsureCacheAsync()
        {
            if (_cache.Count > 0 && (DateTime.Now - _cacheTime) < CacheExpiry) return;
            try
            {
                OverlayLoading.Visibility = Visibility.Visible;
                _cache = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var calcs = db.CostCalculations
                        .Where(c => c.SourceTable == "Kea")
                        .OrderByDescending(c => c.Id)
                        .ToList();

                    var productCodes = calcs.Select(c => c.ProductCode).Distinct().ToList();
                    var campaigns = db.ManualCampaignProducts
                        .Where(mcp => productCodes.Contains(mcp.ProductCode))
                        .Select(mcp => new { mcp.ProductCode, mcp.ManualCampaign.Description })
                        .AsEnumerable()
                        .GroupBy(x => x.ProductCode)
                        .ToDictionary(
                            g => g.Key,
                            g => string.Join("\n\n\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n\n", g.Select(x => x.Description))
                        );

                    var olizCampaigns = db.OlizCampaigns
                        .Where(oc => productCodes.Contains(oc.ProductCode))
                        .Select(oc => new { oc.ProductCode, oc.DiscountAmount, oc.Id })
                        .AsNoTracking()
                        .ToList()
                        .GroupBy(oc => oc.ProductCode)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(oc => oc.Id).First().DiscountAmount,
                            StringComparer.OrdinalIgnoreCase
                        );

                    var keaProducts = db.KeaProducts
                        .Where(p => productCodes.Contains(p.ProductCode))
                        .Select(p => new {
                            p.ProductCode,
                            p.ExcelFileType,
                            CashPrice = p.CashPrice ?? 0,
                            WholesalePrice30 = p.WholesalePrice30 ?? 0,
                            WholesalePrice60 = p.WholesalePrice60 ?? 0,
                            WholesalePrice90 = p.WholesalePrice90 ?? 0,
                            WholesalePrice120 = p.WholesalePrice120 ?? 0
                        })
                        .AsNoTracking()
                        .ToList();

                    var keaDict = keaProducts
                        .Where(x => !string.IsNullOrEmpty(x.ProductCode))
                        .GroupBy(x => x.ProductCode.Trim())
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    return calcs.Select(c =>
                    {
                        decimal cash = 0, w30 = 0, w60 = 0, w90 = 0, w120 = 0;
                        string excelFileType = string.Empty;
                        string key = c.ProductCode?.Trim() ?? string.Empty;
                        if (keaDict.TryGetValue(key, out var kp))
                        {
                            cash = kp.CashPrice;
                            w30 = kp.WholesalePrice30;
                            w60 = kp.WholesalePrice60;
                            w90 = kp.WholesalePrice90;
                            w120 = kp.WholesalePrice120;
                            excelFileType = kp.ExcelFileType;
                        }

                        return new CalculationDisplayItem
                        {
                            Id = c.Id,
                            ProductId = c.ProductId,
                            ProductCode = c.ProductCode,
                            ProductName = c.ProductName,
                            SourceTable = c.SourceTable,
                            PricePP = c.PricePP,
                            PricePPSource = c.PricePPSource,
                            PriceConversion = c.PriceConversion,
                            PurchasePrice = c.PurchasePrice,
                            CardMarkupPercent = c.CardMarkupPercent,
                            CardPurchasePrice = c.CardPurchasePrice,
                            CampaingDate = c.CampaingDate,
                            CreatedDate = c.CreatedDate,
                            ManualCampaignText = campaigns.TryGetValue(c.ProductCode, out var txt) ? txt : string.Empty,
                            DiscountAmount = olizCampaigns.TryGetValue(c.ProductCode, out var disc) ? disc : 0m,

                            CashPrice = cash,
                            WholesalePrice30 = w30,
                            WholesalePrice60 = w60,
                            WholesalePrice90 = w90,
                            WholesalePrice120 = w120,
                            OriginalPricePP = c.PricePP,
                            OriginalPurchasePrice = c.PurchasePrice,
                            ExcelFileType = excelFileType
                        };
                    }).ToList();
                });
                _cacheTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata", $"Veriler y\u00fcklenirken hata olu\u015ftu: {ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                OverlayLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void FilterAndDisplay(string query)
        {
            _currentSearchQuery = query;
            List<CalculationDisplayItem> result;
            if (string.IsNullOrWhiteSpace(query))
            {
                result = _cache;
            }
            else
            {
                string q = query.ToLowerInvariant();
                result = _cache.Where(c =>
                    (c.ProductCode ?? "").ToLowerInvariant().Contains(q) ||
                    (c.ProductName ?? "").ToLowerInvariant().Contains(q)).ToList();
            }
            _filteredData = result.OrderByDescending(c => c.PricePP).ToList();
            _totalCount = _filteredData.Count;
            _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (_totalPages == 0) _totalPages = 1;
            _currentPage = 1;
            UpdatePageDisplay();
        }

        private void UpdatePageDisplay()
        {
            var pagedData = _filteredData
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .Cast<CostCalculation>()
                .ToList();
            GridKea.ItemsSource = pagedData;
            GridKea.Items.Refresh();
            TxtTotalCount.Text = $"Toplam: {_totalCount} \u00dcr\u00fcn";
            TxtPageInfo.Text = $"Sayfa {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < _totalPages;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAndDisplay(TxtSearch.Text.Trim());
        }

        private void CboValorFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridKea == null) return;
            ApplyValorFilter();
            FilterAndDisplay(_currentSearchQuery);
        }

        private void ApplyValorFilter()
        {
            if (CboValorFilter == null || _cache == null || !_cache.Any()) return;

            var selectedItem = CboValorFilter.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string valorKey = selectedItem.Tag?.ToString() ?? "Personal";
            string headerText = selectedItem.Content?.ToString() ?? "Ayarlanan Valörler";
            
            if (ColBasePrice != null)
            {
                ColBasePrice.Header = "Baz Fiyat";
            }

            foreach (var item in _cache)
            {
                string targetValorKey = valorKey;
                if (valorKey == "Personal")
                {
                    // Eğer ürünün kategorisi boş ise varsayılan WholesalePrice60 kullanılır
                    string category = string.IsNullOrEmpty(item.ExcelFileType) ? item.ProductName : item.ExcelFileType;
                    targetValorKey = ValorSettingsService.GetValor(category);
                }

                decimal basePrice = targetValorKey switch
                {
                    "CashPrice" => item.CashPrice,
                    "WholesalePrice30" => item.WholesalePrice30,
                    "WholesalePrice60" => item.WholesalePrice60,
                    "WholesalePrice90" => item.WholesalePrice90,
                    "WholesalePrice120" => item.WholesalePrice120,
                    _ => item.OriginalPricePP
                };

                if (basePrice == 0 && item.OriginalPricePP > 0 && (targetValorKey == "WholesalePrice60" || item.CashPrice == 0))
                {
                    basePrice = item.OriginalPricePP;
                }

                item.PricePP = basePrice;
                item.PurchasePrice = basePrice - item.PriceConversion;
                if (item.PurchasePrice < 0) item.PurchasePrice = 0;
                item.CardPurchasePrice = Math.Round(item.PurchasePrice * (1 + item.CardMarkupPercent / 100m), 2);
                item.ActiveValorText = ValorSettingsService.ValorOptions.TryGetValue(targetValorKey, out var optName) ? optName : "60 Günlük";
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; UpdatePageDisplay(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; UpdatePageDisplay(); }
        }

        // Ctrl + F Kısayolu için
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
            }
        }

        private async void MenuItem_OpenExcel_Click(object sender, RoutedEventArgs e)
        {
            CostCalculation? calc = null;
            if (sender is MenuItem) calc = GridKea.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc != null)
            {
                try
                {
                    using var db = new AppDbContext();
                    int? fileId = null;
                    
                    // 1. Önce ProductId (ID) ile ara
                    if (int.TryParse(calc.ProductId, out int prodId) && prodId > 0)
                    {
                        var prod = db.KeaProducts.FirstOrDefault(x => x.Id == prodId);
                        if (prod != null) fileId = prod.UploadedFileId;
                    }

                    // 2. Bulunamazsa ProductCode ile ara (Daha sağlam)
                    if (fileId == null)
                    {
                        var prod = db.KeaProducts
                            .OrderByDescending(x => x.Id) // En son eklenen aynı kodlu ürünü al
                            .FirstOrDefault(x => x.ProductCode == calc.ProductCode);
                        if (prod != null) fileId = prod.UploadedFileId;
                    }

                    if (fileId.HasValue)
                    {
                        var window = Window.GetWindow(this) as MainWindow;
                        if (window != null)
                        {
                            window.NavigateToPage("ExcelViewer");
                            if (window.MainContentControl.Content is ExcelViewer viewer)
                            {
                                viewer.LoadSpecificFile(fileId.Value);
                            }
                        }
                    }
                    else
                    {
                        await ModernDialogService.ShowAsync("Hata", "Bu ürüne ait kaynak Excel dosyası bulunamadı.", ModernDialogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    await ModernDialogService.ShowAsync("Hata", $"Dosya açılırken hata oluştu: {ex.Message}", ModernDialogType.Error);
                }
            }
        }

        private async void MenuItem_OpenInArcelik_Click(object sender, RoutedEventArgs e)
        {
            CostCalculation? calc = null;
            if (sender is MenuItem) calc = GridKea.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc != null)
            {
                try
                {
                    string searchCode = calc.ProductCode; // KEA için direkt ürün kodunu arat
                    string url = $"https://www.arcelik.com.tr/arama?q={searchCode}";
                    await BrowserHelper.OpenUrlAsync(url);
                }
                catch (Exception ex)
                {
                    await ModernDialogService.ShowAsync("Hata", $"İşlem sırasında bir hata oluştu: {ex.Message}", ModernDialogType.Error);
                }
            }
        }
        private void BtnShowManualCampaign_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CalculationDisplayItem displayItem)
            {
                TxtManualCampaignProductName.Text = $"{displayItem.ProductCode} - {displayItem.ProductName}";
                
                var separator = new[] { "\n\n─────────────────\n\n" };
                var campaigns = displayItem.ManualCampaignText.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(x => x.Trim())
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .ToList();
                                    
                IcManualCampaigns.ItemsSource = campaigns;
                
                ManualCampaignOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseManualCampaign_Click(object sender, RoutedEventArgs e)
        {
            ManualCampaignOverlay.Visibility = Visibility.Collapsed;
        }

        // ── Eski Fiyatları Gör ─────────────────────────────────────────────
        private static readonly string[] _monthNames = {
            "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        private async void MenuItem_ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            CostCalculation? calc = null;
            if (sender is MenuItem) calc = GridKea.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc == null) return;

            try
            {
                OverlayLoading.Visibility = Visibility.Visible;

                string productCode = calc.ProductCode;
                var historyItems = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    return db.HistoricalKeaProducts
                        .Where(h => h.ProductCode == productCode)
                        .OrderByDescending(h => h.PeriodYear)
                        .ThenByDescending(h => h.PeriodMonth)
                        .Take(3)
                        .ToList();
                });

                OverlayLoading.Visibility = Visibility.Collapsed;

                TxtHistoryProductName.Text = $"{calc.ProductCode} - {calc.ProductName}";
                PnlHistoryContainer.Children.Clear();

                if (!historyItems.Any())
                {
                    PnlHistoryContainer.Children.Add(new TextBlock
                    {
                        Text = "Bu ürün için geçmiş fiyat kaydı bulunamadı.",
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = 14,
                        Margin = new Thickness(0, 30, 0, 30)
                    });
                }
                else
                {
                    foreach (var item in historyItems)
                    {
                        string monthName = (item.PeriodMonth >= 1 && item.PeriodMonth <= 12)
                            ? _monthNames[item.PeriodMonth]
                            : item.PeriodMonth.ToString();

                        var border = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFBEB")),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(20, 16, 20, 16),
                            Margin = new Thickness(0, 0, 0, 12),
                            BorderBrush = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FDE68A")),
                            BorderThickness = new Thickness(1)
                        };

                        var grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        // Sol kısım: Dönem bilgisi ve fiyatlar
                        var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                        leftStack.Children.Add(new TextBlock
                        {
                            Text = $"📅 {monthName} {item.PeriodYear}",
                            FontWeight = FontWeights.Bold,
                            FontSize = 16,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#92400E"))
                        });

                        // Alt detaylar
                        var detailsPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

                        if (item.CashPrice.HasValue && item.CashPrice > 0)
                            detailsPanel.Children.Add(CreatePriceRow("Peşin", item.CashPrice.Value));
                        if (item.WholesalePrice30.HasValue && item.WholesalePrice30 > 0)
                            detailsPanel.Children.Add(CreatePriceRow("30 Gün", item.WholesalePrice30.Value));
                        if (item.WholesalePrice60.HasValue && item.WholesalePrice60 > 0)
                            detailsPanel.Children.Add(CreatePriceRow("60 Gün", item.WholesalePrice60.Value));
                        if (item.WholesalePrice90.HasValue && item.WholesalePrice90 > 0)
                            detailsPanel.Children.Add(CreatePriceRow("90 Gün", item.WholesalePrice90.Value));
                        if (item.WholesalePrice120.HasValue && item.WholesalePrice120 > 0)
                            detailsPanel.Children.Add(CreatePriceRow("120 Gün", item.WholesalePrice120.Value));

                        if (detailsPanel.Children.Count == 0)
                            detailsPanel.Children.Add(CreatePriceRow("Fiyat", 0));

                        leftStack.Children.Add(detailsPanel);

                        // Sağ kısım: Ana fiyat büyük
                        var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                        decimal mainPrice = item.WholesalePrice60 ?? item.WholesalePrice90 ?? item.WholesalePrice120 ?? item.WholesalePrice30 ?? item.CashPrice ?? 0;

                        rightStack.Children.Add(new TextBlock
                        {
                            Text = $"{mainPrice:N2} ₺",
                            FontWeight = FontWeights.ExtraBold,
                            FontSize = 20,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706")),
                            HorizontalAlignment = HorizontalAlignment.Right
                        });

                        Grid.SetColumn(leftStack, 0);
                        Grid.SetColumn(rightStack, 1);
                        grid.Children.Add(leftStack);
                        grid.Children.Add(rightStack);
                        border.Child = grid;
                        PnlHistoryContainer.Children.Add(border);
                    }
                }

                HistoryDialogOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                OverlayLoading.Visibility = Visibility.Collapsed;
                await ModernDialogService.ShowAsync("Hata",
                    $"Geçmiş fiyatlar yüklenirken hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
        }

        private static TextBlock CreatePriceRow(string label, decimal price)
        {
            return new TextBlock
            {
                Text = $"{label}: {price:N2} ₺",
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78716C")),
                Margin = new Thickness(0, 2, 0, 0)
            };
        }

        private void BtnCloseHistoryDialog_Click(object sender, RoutedEventArgs e)
        {
            HistoryDialogOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGoToFullHistory_Click(object sender, RoutedEventArgs e)
        {
            HistoryDialogOverlay.Visibility = Visibility.Collapsed;
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
            {
                window.NavigateToPage("Tarihce", "Kea");
            }
        }

        #region Cart
        private CostCalculation? _pendingCartItem;

        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CostCalculation calc)
            {
                _pendingCartItem = calc;
                
                if (calc.PricePP <= 0)
                {
                    TxtManualPrice.Text = string.Empty;
                    ManualPriceOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowCartChoiceOverlay();
                }
            }
        }

        private void ShowCartChoiceOverlay()
        {
            if (_pendingCartItem == null) return;

            TxtParoluPrice.Text = $"{_pendingCartItem.PurchasePrice:N2} ₺";
            TxtParosuzPrice.Text = $"{_pendingCartItem.PricePP:N2} ₺";
            CartChoiceOverlay.Visibility = Visibility.Visible;
        }

        private void BtnConfirmManualPrice_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingCartItem == null) return;

            if (decimal.TryParse(TxtManualPrice.Text.Replace(",", "."), out decimal manualPrice) && manualPrice > 0)
            {
                // Manuel fiyatı PricePP olarak set et, PurchasePrice'ı da buna göre güncelle (kampanya indirimi varsa düş)
                _pendingCartItem.PricePP = manualPrice;
                _pendingCartItem.PurchasePrice = manualPrice - _pendingCartItem.PriceConversion;
                if (_pendingCartItem.PurchasePrice < 0) _pendingCartItem.PurchasePrice = manualPrice;

                ManualPriceOverlay.Visibility = Visibility.Collapsed;
                ShowCartChoiceOverlay();
            }
            else
            {
                _ = ModernDialogService.ShowAsync("Hata", "Lütfen geçerli bir tutar giriniz.", ModernDialogType.Warning);
            }
        }

        private void BtnCancelManualPrice_Click(object sender, RoutedEventArgs e)
        {
            ManualPriceOverlay.Visibility = Visibility.Collapsed;
            _pendingCartItem = null;
        }

        private void BtnCartChoice_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingCartItem == null) return;

            bool isParolu = (sender as Button)?.Tag?.ToString() == "Parolu";
            decimal price = isParolu ? _pendingCartItem.PurchasePrice : _pendingCartItem.PricePP;
            decimal cardPrice = Math.Round(price * (1 + _pendingCartItem.CardMarkupPercent / 100m), 2);

            var cartItem = new CartItem
            {
                ProductCode = _pendingCartItem.ProductCode,
                ProductName = _pendingCartItem.ProductName,
                SelectedPrice = price,
                CardPurchasePrice = cardPrice,
                CardMarkupPercent = _pendingCartItem.CardMarkupPercent,
                IsParolu = isParolu,
                ProductType = "KEA"
            };

            CartService.Instance.AddItem(cartItem);
            CartChoiceOverlay.Visibility = Visibility.Collapsed;
            _pendingCartItem = null;

            // Modern ve animasyonlu Toast mesajı kullan (Alt merkez)
            var window = Window.GetWindow(this) as MainWindow;
            window?.ShowModernToast($"{cartItem.ProductName} sepete eklendi.");
        }

        private void BtnCancelCartChoice_Click(object sender, RoutedEventArgs e)
        {
            CartChoiceOverlay.Visibility = Visibility.Collapsed;
            _pendingCartItem = null;
        }
        #endregion

        private List<ValorSettingRow> _valorRows = new();

        // ViewModel for KEA valor settings overlay
        private class ValorSettingRow : INotifyPropertyChanged
        {
            public string OriginalValorKey { get; set; } = "WholesalePrice60";
            public string CategoryName { get; set; } = string.Empty;

            private string _selectedValorKey = "WholesalePrice60";
            public string SelectedValorKey
            {
                get => _selectedValorKey;
                set
                {
                    _selectedValorKey = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedValorKey)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValorLabel)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChanged)));
                }
            }

            public bool HasChanged => SelectedValorKey != OriginalValorKey;

            public string ValorLabel =>
                ValorSettingsService.ValorOptions.TryGetValue(_selectedValorKey, out var lbl)
                    ? $"Aktif: {lbl}"
                    : "Aktif: 60 Günlük";

            public List<KeyValuePair<string, string>> ValorChoices { get; } =
                ValorSettingsService.ValorOptions.ToList();

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        // ── Valör Ayarları Panel ────────────────────────────────────────────────
        private void BtnOpenValorSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_valorRows == null || !_valorRows.Any())
            {
                _valorRows = ValorSettingsService.KeaCategories
                    .Select(cat =>
                    {
                        var savedValor = ValorSettingsService.GetValor(cat);
                        return new ValorSettingRow
                        {
                            CategoryName     = cat,
                            OriginalValorKey = savedValor,
                            SelectedValorKey = savedValor
                        };
                    })
                    .ToList();
                IcValorSettings.ItemsSource = _valorRows;
            }
            else
            {
                foreach (var row in _valorRows)
                {
                    var savedValor = ValorSettingsService.GetValor(row.CategoryName);
                    row.OriginalValorKey = savedValor;
                    row.SelectedValorKey = savedValor;
                }
            }

            TxtValorSaved.Visibility = Visibility.Collapsed;
            ValorSettingsOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseValorSettings_Click(object sender, RoutedEventArgs e)
        {
            ValorSettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnSaveValorSettings_Click(object sender, RoutedEventArgs e)
        {
            var changedRows = _valorRows.Where(r => r.HasChanged).ToList();

            // Yeni ayarları diske kaydet
            var settings = _valorRows.ToDictionary(r => r.CategoryName, r => r.SelectedValorKey);
            ValorSettingsService.SaveAll(settings);

            if (!changedRows.Any())
            {
                TxtValorSaved.Text = "✔ Değişen bir ayar yok.";
                TxtValorSaved.Visibility = Visibility.Visible;
                await Task.Delay(1500);
                ValorSettingsOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            // Önbelleği temizlemeye veya veritabanından tekrar yüklemeye gerek yok,
            // sadece yeni valör filtresini bellekteki verilere uygula.
            ApplyValorFilter();
            FilterAndDisplay(_currentSearchQuery);

            ValorSettingsOverlay.Visibility = Visibility.Collapsed;
            await ModernDialogService.ShowAsync(
                "Maliyet Güncellendi",
                "Valör ayarlarınız kaydedildi ve görünüm güncellendi.",
                ModernDialogType.Success);
        }
    }
}
