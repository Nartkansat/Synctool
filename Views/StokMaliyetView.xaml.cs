using Synctool.Data;
using Synctool.DTOs;
using Synctool.Services;
using Synctool.Models;
using Synctool.Services;
using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace Synctool.Views
{
    public partial class StokMaliyetView : UserControl
    {
        private List<BeyazEsyaListItemDto> _dbProducts = new();
        private List<StockCalculationRow> _calculatedRows = new();
        private string _selectedFilePath = string.Empty;

        public StokMaliyetView()
        {
            InitializeComponent();
            ExcelPackage.License.SetNonCommercialPersonal("NART");
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadCatalogAsync();
        }

        private async Task LoadCatalogAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                TxtLoadingMessage.Text = "Veritabanı Kataloğu Yükleniyor...";

                _dbProducts = await Task.Run(() =>
                {
                    using var db = new AppDbContext();

                    // 1. Fetch all Cost Calculations
                    var calcs = db.CostCalculations
                        .OrderByDescending(c => c.Id)
                        .AsNoTracking()
                        .ToList();

                    if (!calcs.Any())
                        return new List<BeyazEsyaListItemDto>();

                    var productCodes = calcs.Select(c => c.ProductCode).Distinct().ToList();

                    // 2. Fetch White Goods products to map valor prices
                    var wgProducts = db.WhiteGoodsProducts
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

                    // 3. Fetch KEA products to map valor prices
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

                    var wgDict = wgProducts.GroupBy(x => x.ProductCode).ToDictionary(g => g.Key, g => g.First());
                    var keaDict = keaProducts.GroupBy(x => x.ProductCode).ToDictionary(g => g.Key, g => g.First());

                    return calcs.Select(c =>
                    {
                        decimal cash = 0, w30 = 0, w60 = 0, w90 = 0, w120 = 0;
                        string excelFileType = string.Empty;
                        if (c.SourceTable == "WhiteGoods" && wgDict.TryGetValue(c.ProductCode, out var wg))
                        {
                            cash = wg.CashPrice;
                            w30 = wg.WholesalePrice30;
                            w60 = wg.WholesalePrice60;
                            w90 = wg.WholesalePrice90;
                            w120 = wg.WholesalePrice120;
                            excelFileType = wg.ExcelFileType;
                        }
                        else if (c.SourceTable == "Kea" && keaDict.TryGetValue(c.ProductCode, out var kea))
                        {
                            cash = kea.CashPrice;
                            w30 = kea.WholesalePrice30;
                            w60 = kea.WholesalePrice60;
                            w90 = kea.WholesalePrice90;
                            w120 = kea.WholesalePrice120;
                            excelFileType = kea.ExcelFileType;
                        }

                        return new BeyazEsyaListItemDto
                        {
                            Id = c.Id,
                            ProductId = c.ProductId,
                            ProductCode = c.ProductCode,
                            ProductName = c.ProductName,
                            SourceTable = c.SourceTable,
                            PricePPSource = c.PricePPSource,
                            PricePP = c.PricePP,
                            PriceConversion = c.PriceConversion,
                            PurchasePrice = c.PurchasePrice,
                            CardMarkupPercent = c.CardMarkupPercent,
                            CardPurchasePrice = c.CardPurchasePrice,
                            CampaingDate = c.CampaingDate,
                            ExcelFileType = excelFileType,
                            CreatedDate = c.CreatedDate,
                            CashPrice = cash,
                            WholesalePrice30 = w30,
                            WholesalePrice60 = w60,
                            WholesalePrice90 = w90,
                            WholesalePrice120 = w120
                        };
                    }).ToList();
                });
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata", $"Sistem ürün kataloğu yüklenemedi: {ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Drag & Drop Excel ───────────────────────────────────────────────
        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string file = files.FirstOrDefault(x => x.EndsWith(".xlsx") || x.EndsWith(".xls") || x.EndsWith(".xlsm"));
                if (!string.IsNullOrEmpty(file))
                {
                    LoadExcelHeaders(file);
                }
            }
        }

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Dosyaları|*.xlsx;*.xls;*.xlsm",
                Title = "Excel Dosyası Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadExcelHeaders(openFileDialog.FileName);
            }
        }

        private void LoadExcelHeaders(string filePath)
        {
            try
            {
                _selectedFilePath = filePath;
                TxtSelectedFile.Text = Path.GetFileName(filePath);

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    if (package.Workbook.Worksheets.Count == 0)
                        throw new Exception("Excel dosyası geçerli bir çalışma sayfası içermiyor.");

                    var ws = package.Workbook.Worksheets[0];
                    if (ws.Dimension == null)
                        throw new Exception("Excel sayfası boş.");

                    CmbCodeColumn.Items.Clear();
                    CmbStockColumn.Items.Clear();

                    int codeGuessIndex = -1;
                    int stockGuessIndex = -1;

                    for (int col = 1; col <= ws.Dimension.End.Column; col++)
                    {
                        string header = ws.Cells[1, col].Text?.Trim() ?? "";
                        if (string.IsNullOrEmpty(header)) header = $"Kolon {col}";

                        CmbCodeColumn.Items.Add(header);
                        CmbStockColumn.Items.Add(header);

                        string lowerHeader = header.ToLowerInvariant();
                        if (codeGuessIndex == -1 && (lowerHeader.Contains("kod") || lowerHeader.Contains("code") || lowerHeader.Contains("ürün") || lowerHeader.Contains("model")))
                        {
                            codeGuessIndex = col - 1;
                        }
                        if (stockGuessIndex == -1 && (lowerHeader.Contains("stok") || lowerHeader.Contains("adet") || lowerHeader.Contains("qty") || lowerHeader.Contains("sayı") || lowerHeader.Contains("miktar") || lowerHeader.Contains("quantity")))
                        {
                            stockGuessIndex = col - 1;
                        }
                    }

                    CmbCodeColumn.IsEnabled = true;
                    CmbStockColumn.IsEnabled = true;
                    BtnCalculate.IsEnabled = true;

                    if (codeGuessIndex != -1) CmbCodeColumn.SelectedIndex = codeGuessIndex;
                    if (stockGuessIndex != -1) CmbStockColumn.SelectedIndex = stockGuessIndex;
                }
            }
            catch (Exception ex)
            {
                _ = ModernDialogService.ShowAsync("Excel Okuma Hatası", $"Excel sütun başlıkları okunurken hata oluştu:\n{ex.Message}", ModernDialogType.Error);
                _selectedFilePath = string.Empty;
                TxtSelectedFile.Text = string.Empty;
                CmbCodeColumn.IsEnabled = false;
                CmbStockColumn.IsEnabled = false;
                BtnCalculate.IsEnabled = false;
            }
        }

        // ─── Calculate ───────────────────────────────────────────────────────
        private async void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                await ModernDialogService.ShowAsync("Uyarı", "Lütfen önce geçerli bir Excel dosyası seçin.", ModernDialogType.Warning);
                return;
            }

            if (CmbCodeColumn.SelectedIndex == -1 || CmbStockColumn.SelectedIndex == -1)
            {
                await ModernDialogService.ShowAsync("Uyarı", "Lütfen Ürün Kodu ve Stok Adedi sütunlarını seçin.", ModernDialogType.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            TxtLoadingMessage.Text = "Hesaplanıyor...";

            int codeCol = CmbCodeColumn.SelectedIndex + 1;
            int stockCol = CmbStockColumn.SelectedIndex + 1;
            string valorKey = (CmbValor.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "WholesalePrice60";
            string priceType = (CmbPriceType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Discounted";

            try
            {
                await Task.Run(() =>
                {
                    var resultList = new List<StockCalculationRow>();

                    using (var package = new ExcelPackage(new FileInfo(_selectedFilePath)))
                    {
                        var ws = package.Workbook.Worksheets[0];
                        int totalRows = ws.Dimension?.End.Row ?? 1;

                        for (int row = 2; row <= totalRows; row++)
                        {
                            string code = ws.Cells[row, codeCol].Text?.Trim() ?? "";
                            if (string.IsNullOrEmpty(code)) continue;

                            object stockVal = ws.Cells[row, stockCol].Value;
                            double stock = ParseStockValue(stockVal);

                            // Match product in catalog
                            var dbProduct = _dbProducts.FirstOrDefault(p =>
                                string.Equals(p.ProductCode?.Trim(), code, StringComparison.OrdinalIgnoreCase));

                            decimal unitPrice = 0;
                            string dbProdName = "";
                            bool matched = dbProduct != null;

                            if (matched && dbProduct != null)
                            {
                                dbProdName = dbProduct.ProductName;

                                // Determine base price from selected valor
                                string targetValorKey = valorKey;
                                if (valorKey == "Personal")
                                {
                                    string category = string.IsNullOrEmpty(dbProduct.ExcelFileType) ? dbProduct.ProductName : dbProduct.ExcelFileType;
                                    targetValorKey = ValorSettingsService.GetValor(category);
                                }

                                decimal basePrice = targetValorKey switch
                                {
                                    "CashPrice" => dbProduct.CashPrice,
                                    "WholesalePrice30" => dbProduct.WholesalePrice30,
                                    "WholesalePrice60" => dbProduct.WholesalePrice60,
                                    "WholesalePrice90" => dbProduct.WholesalePrice90,
                                    "WholesalePrice120" => dbProduct.WholesalePrice120,
                                    _ => dbProduct.WholesalePrice60
                                };

                                // Fallback pricing
                                if (basePrice == 0 && dbProduct.PricePP > 0 && (targetValorKey == "WholesalePrice60" || dbProduct.CashPrice == 0))
                                {
                                    basePrice = dbProduct.PricePP;
                                }

                                if (priceType == "Discounted")
                                {
                                    unitPrice = basePrice - dbProduct.PriceConversion;
                                    if (unitPrice < 0) unitPrice = 0;
                                }
                                else
                                {
                                    unitPrice = basePrice;
                                }
                            }

                            resultList.Add(new StockCalculationRow
                            {
                                ExcelProductCode = code,
                                ExcelStockCount = stock,
                                IsMatched = matched,
                                DbProductName = dbProdName,
                                UnitPrice = unitPrice,
                                TotalCost = unitPrice * (decimal)stock
                            });
                        }
                    }

                    _calculatedRows = resultList;
                });

                // Display Results & Update Statistics
                GridPreview.ItemsSource = null;
                GridPreview.ItemsSource = _calculatedRows;

                TxtTotalRows.Text = _calculatedRows.Count.ToString();
                TxtMatchedRows.Text = _calculatedRows.Count(x => x.IsMatched).ToString();
                TxtUnmatchedRows.Text = _calculatedRows.Count(x => !x.IsMatched).ToString();

                decimal grandTotal = _calculatedRows.Sum(x => x.TotalCost);
                TxtTotalValue.Text = $"{grandTotal:N2} ₺";

                TxtGridSummary.Text = $"Hesaplama tamamlandı: {_calculatedRows.Count} satır işlendi.";
                BtnExport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hesaplama Hatası", $"Stok maliyeti hesaplanırken bir hata oluştu:\n{ex.Message}", ModernDialogType.Error);
                BtnExport.IsEnabled = false;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Search ──────────────────────────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_calculatedRows == null || !_calculatedRows.Any()) return;

            string query = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                GridPreview.ItemsSource = _calculatedRows;
            }
            else
            {
                GridPreview.ItemsSource = _calculatedRows.Where(r =>
                    r.ExcelProductCode.ToLowerInvariant().Contains(query) ||
                    r.DbProductName.ToLowerInvariant().Contains(query)
                ).ToList();
            }
        }

        // ─── Export ──────────────────────────────────────────────────────────
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !_calculatedRows.Any()) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Dosyaları (*.xlsx)|*.xlsx",
                FileName = $"{Path.GetFileNameWithoutExtension(_selectedFilePath)}_Maliyetli.xlsx",
                Title = "Stok Maliyet Raporunu Kaydet"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                TxtLoadingMessage.Text = "Excel Dosyası Dışa Aktarılıyor...";

                try
                {
                    string targetPath = saveFileDialog.FileName;
                    int codeCol = CmbCodeColumn.SelectedIndex + 1;

                    await Task.Run(() =>
                    {
                        // Copy original to preserve all other sheets, styles, formulas and columns
                        File.Copy(_selectedFilePath, targetPath, true);

                        using (var package = new ExcelPackage(new FileInfo(targetPath)))
                        {
                            var ws = package.Workbook.Worksheets[0];
                            int lastCol = ws.Dimension.End.Column;

                            // Add Extra Columns
                            ws.Cells[1, lastCol + 1].Value = "Birim Fiyat";
                            ws.Cells[1, lastCol + 2].Value = "Toplam Tutar";

                            // Style headers
                            ws.Cells[1, lastCol + 1].Style.Font.Bold = true;
                            ws.Cells[1, lastCol + 2].Style.Font.Bold = true;

                            for (int row = 2; row <= ws.Dimension.End.Row; row++)
                            {
                                string code = ws.Cells[row, codeCol].Text?.Trim() ?? "";
                                if (string.IsNullOrEmpty(code)) continue;

                                var calculated = _calculatedRows.FirstOrDefault(r =>
                                    string.Equals(r.ExcelProductCode, code, StringComparison.OrdinalIgnoreCase));

                                if (calculated != null)
                                {
                                    ws.Cells[row, lastCol + 1].Value = (double)calculated.UnitPrice;
                                    ws.Cells[row, lastCol + 2].Value = (double)calculated.TotalCost;
                                }
                                else
                                {
                                    ws.Cells[row, lastCol + 1].Value = 0;
                                    ws.Cells[row, lastCol + 2].Value = 0;
                                }

                                // Format columns as currency
                                ws.Cells[row, lastCol + 1].Style.Numberformat.Format = "#,##0.00\" ₺\"";
                                ws.Cells[row, lastCol + 2].Style.Numberformat.Format = "#,##0.00\" ₺\"";
                            }

                            package.Save();
                        }
                    });

                    await ModernDialogService.ShowAsync("Başarılı", "Excel dosyası başarıyla dışa aktarıldı ve kaydedildi.", ModernDialogType.Success);
                }
                catch (Exception ex)
                {
                    await ModernDialogService.ShowAsync("Hata", $"Dosya kaydedilirken hata oluştu: {ex.Message}", ModernDialogType.Error);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private double ParseStockValue(object val)
        {
            if (val == null) return 0;

            if (val is double d) return d;
            if (val is int i) return i;
            if (val is decimal dec) return (double)dec;
            if (val is float f) return f;
            if (val is long l) return l;

            string str = val.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(str)) return 0;

            // Try parsing with CurrentCulture
            if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double res))
                return res;

            // Try parsing with InvariantCulture (dot as decimal)
            if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out res))
                return res;

            // If it contains a comma and failed current culture, try swapping comma to dot or vice versa
            string swapped = str.Contains(",") ? str.Replace(",", ".") : str.Replace(".", ",");
            if (double.TryParse(swapped, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out res))
                return res;

            return 0;
        }
    }

    public class StockCalculationRow
    {
        public string ExcelProductCode { get; set; } = string.Empty;
        public double ExcelStockCount { get; set; }
        public bool IsMatched { get; set; }
        public string DbProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal TotalCost { get; set; }
    }
}
