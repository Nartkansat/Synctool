using ArcelikApp.Data;
using ArcelikApp.Models;
using ArcelikApp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ArcelikExcelApp.Models;
using ArcelikExcelApp.Services;
using MaterialDesignThemes.Wpf;

namespace ArcelikExcelApp.Views
{
    public partial class BeyazEsyaView : UserControl
    {
        private List<ValorSettingRow> _valorRows = new();

        // Statik önbellek — view kapatılıp açılsa bile veri kaybolmaz, anlık çalışır
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

        // Debounce için
        private CancellationTokenSource? _searchCts;

        public class CalculationDisplayItem : CostCalculation
        {
            public string ManualCampaignText { get; set; } = string.Empty;
            public bool HasManualCampaign => !string.IsNullOrEmpty(ManualCampaignText);
        }


        // ViewModel for valor settings overlay
        private class ValorSettingRow : INotifyPropertyChanged
        {
            // Paneli açtığımızdaki orijinal değer — değişen kategorileri tespit için
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

        public BeyazEsyaView()
        {
            InitializeComponent();

            if (AuthService.CurrentUser != null)
            {
              
                // Admin değilse admin bölümünü gizle
                if (AuthService.CurrentUser.Role != "Admin")
                {
                    BtnOpenValorSettings.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = InitAsync();
            this.Focus();
        }

        // İlk açılışta ve önbellek dolmadıysa DB'den yükler, sonra anlık filtreler
        private async Task InitAsync()
        {
            await EnsureCacheAsync();
            if (_cache.Count > 0)
                ColCardPrice.Header = $"Kart Fiyat\u0131 (%{Convert.ToInt32(_cache[0].CardMarkupPercent)})";
            FilterAndDisplay(_currentSearchQuery);
        }

        // Önbelleği doldurur — sadece boşsa veya süresi dolmuşsa DB'ye gider
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
                        .Where(c => c.SourceTable == "WhiteGoods")
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

                    return calcs.Select(c => new CalculationDisplayItem
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
                        ManualCampaignText = campaigns.TryGetValue(c.ProductCode, out var txt) ? txt : string.Empty
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

        // Bellekte anında filtrele ve sayfayı güncelle
        private async void FilterAndDisplay(string query)
        {
            _currentSearchQuery = query;

            List<CalculationDisplayItem> result;

            if (string.IsNullOrWhiteSpace(query))
            {
                result = _cache;
            }
            else
            {
                // LINQ filtrelemesini UI thread'i dışında yap — büyük listede takılmayı önler
                string q = query.ToLowerInvariant();
                result = await Task.Run(() =>
                    _cache.Where(c =>
                        (c.ProductCode ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        (c.ProductName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                    ).ToList()
                );
            }

            _filteredData = result;
            _totalCount = _filteredData.Count;
            _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
            if (_totalPages == 0) _totalPages = 1;
            _currentPage = 1;
            UpdatePageDisplay();
        }

        // Mevcut sayfayı grid'e bağla
        private void UpdatePageDisplay()
        {
            var pagedData = _filteredData
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .Cast<CostCalculation>()
                .ToList();
            GridBeyazEsya.ItemsSource = pagedData;
            TxtTotalCount.Text = $"Toplam: {_totalCount} \u00dcr\u00fcn";
            TxtPageInfo.Text = $"Sayfa {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < _totalPages;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce: kullanıcı yazmayı bırakana kadar (200ms) beklenir
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            var query = TxtSearch.Text.Trim();

            _ = Task.Delay(200, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                Dispatcher.Invoke(() => FilterAndDisplay(query));
            }, TaskScheduler.Default);
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
            if (sender is MenuItem) calc = GridBeyazEsya.SelectedItem as CostCalculation;
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
                        var prod = db.WhiteGoodsProducts.FirstOrDefault(x => x.Id == prodId);
                        if (prod != null) fileId = prod.UploadedFileId;
                    }

                    // 2. Bulunamazsa ProductCode ile ara (Daha sağlam)
                    if (fileId == null)
                    {
                        var prod = db.WhiteGoodsProducts
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
            if (sender is MenuItem) calc = GridBeyazEsya.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc != null)
            {
                try
                {
                    string searchCode = calc.ProductCode; // Default olarak ProductCode
                    
                    // Veritabanından WhiteGoodsProduct çekilip Klima ise Description alınacak
                    using (var db = new AppDbContext())
                    {
                        WhiteGoodsProduct? wgProduct = null;
                        if (int.TryParse(calc.ProductId, out int prodId) && prodId > 0)
                        {
                            wgProduct = db.WhiteGoodsProducts.FirstOrDefault(x => x.Id == prodId);
                        }
                        
                        if (wgProduct == null)
                        {
                            wgProduct = db.WhiteGoodsProducts.OrderByDescending(x => x.Id).FirstOrDefault(x => x.ProductCode == calc.ProductCode);
                        }

                        if (wgProduct != null && wgProduct.ExcelFileType == "Klima" && !string.IsNullOrWhiteSpace(wgProduct.Description))
                        {
                            searchCode = wgProduct.Description; // Dış Ünite SKU
                        }
                    }

                    string url = $"https://www.arcelik.com.tr/arama?q={searchCode}";
                    await BrowserHelper.OpenUrlAsync(url);
                }
                catch (Exception ex)
                {
                    await ModernDialogService.ShowAsync("Hata", $"İşlem sırasında bir hata oluştu: {ex.Message}", ModernDialogType.Error);
                }
            }
        }

        private async void MenuItem_ViewAllValors_Click(object sender, RoutedEventArgs e)
        {
            CostCalculation? calc = null;
            if (sender is MenuItem) calc = GridBeyazEsya.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc != null)
            {
                try
                {
                    OverlayLoading.Visibility = Visibility.Visible;
                    
                    WhiteGoodsProduct? wgProduct = null;
                    await Task.Run(() =>
                    {
                        using var db = new AppDbContext();
                        // Önce ID ile ara
                        if (int.TryParse(calc.ProductId, out int prodId) && prodId > 0)
                        {
                            wgProduct = db.WhiteGoodsProducts.FirstOrDefault(x => x.Id == prodId);
                        }
                        
                        // Bulunamazsa Ürün Kodu ile en güncelini al
                        if (wgProduct == null)
                        {
                            wgProduct = db.WhiteGoodsProducts
                                .OrderByDescending(x => x.Id)
                                .FirstOrDefault(x => x.ProductCode == calc.ProductCode);
                        }
                    });

                    OverlayLoading.Visibility = Visibility.Collapsed;

                    if (wgProduct == null)
                    {
                        await ModernDialogService.ShowAsync("Hata", "Bu ürüne ait kaynak detaylar veritabanında bulunamadı.", ModernDialogType.Warning);
                        return;
                    }

                    TxtValorProductName.Text = $"{calc.ProductCode} - {calc.ProductName}";
                    PnlValorContainer.Children.Clear();

                    var valors = new[]
                    {
                        new { Label = wgProduct.ExcelFileType == "Klima" ? "30 Günlük" : "30 Günlük", Price = wgProduct.WholesalePrice30 },
                        new { Label = wgProduct.ExcelFileType == "Klima" ? "Y060" : "60 Günlük", Price = wgProduct.WholesalePrice60 },
                        new { Label = wgProduct.ExcelFileType == "Klima" ? "Y90" : "90 Günlük", Price = wgProduct.WholesalePrice90 },
                        new { Label = wgProduct.ExcelFileType == "Klima" ? "Y120" : "120 Günlük", Price = wgProduct.WholesalePrice120 }
                    };

                    bool hasAnyData = false;

                    foreach (var valor in valors)
                    {
                        if (valor.Price.HasValue && valor.Price.Value > 0)
                        {
                            hasAnyData = true;
                            
                            // Hesaplamalar
                            decimal priceConversion = calc.PriceConversion; // İndirim miktarı
                            decimal purchasePrice = valor.Price.Value - priceConversion;
                            decimal finalCost = Math.Round(purchasePrice * (1 + calc.CardMarkupPercent / 100m), 2);

                            // UI Elemanlarını Oluştur
                            var border = new Border
                            {
                                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1F5F9")),
                                CornerRadius = new CornerRadius(12),
                                Padding = new Thickness(20, 15, 20, 15),
                                Margin = new Thickness(0, 0, 0, 12)
                            };

                            var grid = new Grid();
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                            titleStack.Children.Add(new TextBlock 
                            { 
                                Text = $"{valor.Label} Valör Maliyeti", 
                                FontWeight = FontWeights.Bold, 
                                FontSize = 15, 
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")) 
                            });
                            
                            titleStack.Children.Add(new TextBlock 
                            { 
                                Text = $"Baz: {valor.Price.Value:N2} ₺ | İndirim: {priceConversion:N2} ₺", 
                                FontSize = 12, 
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                                Margin = new Thickness(0, 4, 0, 0)
                            });

                            titleStack.Children.Add(new TextBlock 
                            { 
                                Text = $"Net Maliyet: {purchasePrice:N2} ₺", 
                                FontWeight = FontWeights.Bold,
                                FontSize = 14, 
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981")), // Yeşil renk
                                Margin = new Thickness(0, 6, 0, 0)
                            });

                            var costStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                            
                            costStack.Children.Add(new TextBlock
                            {
                                Text = "Kredi Kartı",
                                FontSize = 12,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Margin = new Thickness(0, 0, 0, 2)
                            });

                            costStack.Children.Add(new TextBlock
                            {
                                Text = $"{finalCost:N2} ₺",
                                FontWeight = FontWeights.ExtraBold,
                                FontSize = 18,
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E02020")),
                                HorizontalAlignment = HorizontalAlignment.Right
                            });
                            
                            costStack.Children.Add(new TextBlock
                            {
                                Text = $"%{(int)calc.CardMarkupPercent} Komisyon",
                                FontSize = 11,
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Margin = new Thickness(0, 2, 0, 0)
                            });

                            Grid.SetColumn(titleStack, 0);
                            Grid.SetColumn(costStack, 1);

                            grid.Children.Add(titleStack);
                            grid.Children.Add(costStack);
                            border.Child = grid;

                            PnlValorContainer.Children.Add(border);
                        }
                    }

                    if (!hasAnyData)
                    {
                        PnlValorContainer.Children.Add(new TextBlock
                        {
                            Text = "Bu ürün için tanımlanmış ek valör fiyatları bulunamadı.",
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            FontSize = 14,
                            Margin = new Thickness(0, 30, 0, 30)
                        });
                    }

                    ValorDialogOverlay.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    OverlayLoading.Visibility = Visibility.Collapsed;
                    await ModernDialogService.ShowAsync("Hata", $"Valörler hesaplanırken beklenmeyen bir hata oluştu:\n{ex.Message}", ModernDialogType.Error);
                }
            }
        }

        private void BtnCloseValorDialog_Click(object sender, RoutedEventArgs e)
        {
            ValorDialogOverlay.Visibility = Visibility.Collapsed;
        }

        // ── Valör Ayarları Panel ────────────────────────────────────────────────
        private void BtnOpenValorSettings_Click(object sender, RoutedEventArgs e)
        {
            _valorRows = ValorSettingsService.WhiteGoodsCategories
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

            IcValorSettings.ItemsSource = null;
            IcValorSettings.ItemsSource = _valorRows;
            TxtValorSaved.Visibility = Visibility.Collapsed;
            ValorSettingsOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseValorSettings_Click(object sender, RoutedEventArgs e)
        {
            ValorSettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnSaveValorSettings_Click(object sender, RoutedEventArgs e)
        {
            // Değişen kategorileri bul
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

            // Yükleniyor göstergesi
            ValorSettingsOverlay.Visibility = Visibility.Collapsed;
            OverlayLoading.Visibility = Visibility.Visible;

            try
            {
                int totalUpdated = 0;

                await Task.Run(() =>
                {
                    using var db = new AppDbContext();

                    foreach (var row in changedRows)
                    {
                        string category  = row.CategoryName;
                        string valorKey  = row.SelectedValorKey;

                        // Bu kategorideki tüm WhiteGoodsProduct’ları al
                        var wgProducts = db.WhiteGoodsProducts
                            .Where(w => w.ExcelFileType == category)
                            .ToList();

                        if (!wgProducts.Any()) continue;

                        var byId   = wgProducts.ToDictionary(w => w.Id.ToString());
                        var byCode = wgProducts
                            .GroupBy(w => w.ProductCode)
                            .ToDictionary(g => g.Key, g => g.First());

                        // Bu ürünlere ait CostCalculation kayıtlarını al
                        var productIds   = byId.Keys.ToHashSet();
                        var productCodes = byCode.Keys.ToHashSet();

                        var calcs = db.CostCalculations
                            .Where(c => c.SourceTable == "WhiteGoods" &&
                                       (productIds.Contains(c.ProductId) ||
                                        productCodes.Contains(c.ProductCode)))
                            .ToList();

                        foreach (var calc in calcs)
                        {
                            // WhiteGoodsProduct’ı bul
                            WhiteGoodsProduct? wg = null;
                            if (byId.TryGetValue(calc.ProductId, out var p1))   wg = p1;
                            else if (byCode.TryGetValue(calc.ProductCode, out var p2)) wg = p2;
                            if (wg == null) continue;

                            decimal newPricePP = GetPriceFromValor(wg, valorKey);
                            if (newPricePP == 0) continue; // Bu valör bu ürün için tanımlı değil

                            calc.PricePPSource     = valorKey;
                            calc.PricePP           = newPricePP;
                            calc.PurchasePrice     = newPricePP - calc.PriceConversion;
                            calc.CardPurchasePrice  = Math.Round(
                                calc.PurchasePrice * (1 + calc.CardMarkupPercent / 100m), 2);

                            totalUpdated++;
                        }

                        db.SaveChanges();
                    }
                });

                // Ekranı yenile
                _cache.Clear(); // Önbelleği sıfırla, yeni veriler yüklensin
                await InitAsync();

                await ModernDialogService.ShowAsync(
                    "Maliyet Güncellendi",
                    $"{changedRows.Count} kategori için toplam {totalUpdated} kayıt yeni valöre göre yeniden hesaplandı ve güncellendi.",
                    ModernDialogType.Success);
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata",
                    $"Güncelleme sırasında hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                OverlayLoading.Visibility = Visibility.Collapsed;
            }
        }

        private static decimal GetPriceFromValor(WhiteGoodsProduct wg, string valorKey) => valorKey switch
        {
            "WholesalePrice30"  => wg.WholesalePrice30  ?? 0m,
            "WholesalePrice60"  => wg.WholesalePrice60  ?? 0m,
            "WholesalePrice90"  => wg.WholesalePrice90  ?? 0m,
            "WholesalePrice120" => wg.WholesalePrice120 ?? 0m,
            "CashPrice"         => wg.CashPrice         ?? 0m,
            _                   => wg.WholesalePrice60  ?? 0m
        };

        // ── Eski Fiyatları Gör ─────────────────────────────────────────────
        private static readonly string[] _monthNames = {
            "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        private async void MenuItem_ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            CostCalculation? calc = null;
            if (sender is MenuItem) calc = GridBeyazEsya.SelectedItem as CostCalculation;
            else if (sender is Button btn) calc = btn.DataContext as CostCalculation;

            if (calc == null) return;

            try
            {
                OverlayLoading.Visibility = Visibility.Visible;

                string productCode = calc.ProductCode;
                var historyItems = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    return db.HistoricalWhiteGoodsProducts
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
                window.NavigateToPage("Tarihce", "BeyazEsya");
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
                ProductType = "BeyazEsya"
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
    }
}

