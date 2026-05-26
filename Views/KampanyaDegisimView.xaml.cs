using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OfficeOpenXml;
using Synctool.Services;
using MaterialDesignThemes.Wpf;

namespace Synctool.Views
{
    public partial class KampanyaDegisimView : UserControl
    {
        private string _file1Path = string.Empty;
        private string _file2Path = string.Empty;

        public KampanyaDegisimView()
        {
            InitializeComponent();
            ExcelPackage.License.SetNonCommercialPersonal("Synctool");
            ViewSnackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        }

        #region File 1 Handlers
        private void Border1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                SetFile1(openFileDialog.FileName);
            }
        }

        private void Border1_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    SetFile1(files[0]);
                }
            }
        }

        private void SetFile1(string filePath)
        {
            _file1Path = filePath;
            TxtFile1.Text = Path.GetFileName(filePath);
            LoadSheets(filePath, CmbSheet1);
            Options1.Visibility = Visibility.Visible;
            ValidateAnalyzeButton();
        }

        private void CmbSheet1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbSheet1.SelectedItem is string sheetName)
            {
                LoadColumns(_file1Path, sheetName, CmbCode1, CmbName1, CmbPrice1);
            }
        }
        #endregion

        #region File 2 Handlers
        private void Border2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                SetFile2(openFileDialog.FileName);
            }
        }

        private void Border2_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    SetFile2(files[0]);
                }
            }
        }

        private void SetFile2(string filePath)
        {
            _file2Path = filePath;
            TxtFile2.Text = Path.GetFileName(filePath);
            LoadSheets(filePath, CmbSheet2);
            Options2.Visibility = Visibility.Visible;
            ValidateAnalyzeButton();
        }

        private void CmbSheet2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbSheet2.SelectedItem is string sheetName)
            {
                LoadColumns(_file2Path, sheetName, CmbCode2, CmbName2, CmbPrice2);
            }
        }
        #endregion

        private void LoadSheets(string filePath, ComboBox cmb)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    cmb.ItemsSource = package.Workbook.Worksheets.Select(x => x.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                ViewSnackbar.MessageQueue?.Enqueue("Hata: " + ex.Message);
            }
        }

        private void LoadColumns(string filePath, string sheetName, ComboBox cmbCode, ComboBox cmbName, ComboBox cmbPrice)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[sheetName];
                    if (worksheet == null) return;

                    var headers = new List<string>();
                    for (int i = 1; i <= worksheet.Dimension.End.Column; i++)
                    {
                        var cellValue = worksheet.Cells[1, i].Value?.ToString();
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            headers.Add(cellValue);
                        }
                    }

                    cmbCode.ItemsSource = headers;
                    cmbName.ItemsSource = headers;
                    cmbPrice.ItemsSource = headers;

                    cmbCode.SelectedItem = headers.FirstOrDefault(h => h.ToLower().Contains("kod") || h.ToLower().Contains("sku"));
                    cmbName.SelectedItem = headers.FirstOrDefault(h => h.ToLower().Contains("ad") || h.ToLower().Contains("tanım"));
                    cmbPrice.SelectedItem = headers.FirstOrDefault(h => h.ToLower().Contains("fiyat") || h.ToLower().Contains("tutar"));
                }
            }
            catch (Exception ex)
            {
                ViewSnackbar.MessageQueue?.Enqueue("Hata: " + ex.Message);
            }
        }

        private void ValidateAnalyzeButton()
        {
            BtnAnalyze.IsEnabled = !string.IsNullOrEmpty(_file1Path) && !string.IsNullOrEmpty(_file2Path);
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (CmbSheet1.SelectedItem == null || CmbCode1.SelectedItem == null || CmbName1.SelectedItem == null || CmbPrice1.SelectedItem == null ||
                CmbSheet2.SelectedItem == null || CmbCode2.SelectedItem == null || CmbName2.SelectedItem == null || CmbPrice2.SelectedItem == null)
            {
                ViewSnackbar.MessageQueue?.Enqueue("Lütfen tüm alanları doldurun.");
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            BtnAnalyze.IsEnabled = false;

            string sheet1 = CmbSheet1.SelectedItem?.ToString()!;
            string code1 = CmbCode1.SelectedItem?.ToString()!;
            string name1 = CmbName1.SelectedItem?.ToString()!;
            string price1 = CmbPrice1.SelectedItem?.ToString()!;
            
            string sheet2 = CmbSheet2.SelectedItem?.ToString()!;
            string code2 = CmbCode2.SelectedItem?.ToString()!;
            string name2 = CmbName2.SelectedItem?.ToString()!;
            string price2 = CmbPrice2.SelectedItem?.ToString()!;

            try
            {
                var results = await Task.Run(() => PerformComparison(sheet1, code1, name1, price1, sheet2, code2, name2, price2));
                GridResults.ItemsSource = results;
                BtnExport.Visibility = results.Any() ? Visibility.Visible : Visibility.Collapsed;
                
                if (!results.Any())
                {
                    EmptyState.Visibility = Visibility.Visible;
                    ViewSnackbar.MessageQueue?.Enqueue("Eşleşen ürün bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                ViewSnackbar.MessageQueue?.Enqueue("Hata: " + ex.Message);
                EmptyState.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnAnalyze.IsEnabled = true;
            }
        }

        private List<CampaignComparisonResult> PerformComparison(string sheet1, string code1, string name1, string price1, string sheet2, string code2, string name2, string price2)
        {
            var results = new List<CampaignComparisonResult>();
            var data1 = ReadExcelData(_file1Path, sheet1, code1, name1, price1);
            var data2 = ReadExcelData(_file2Path, sheet2, code2, name2, price2);

            foreach (var item2 in data2)
            {
                string code = item2.Key;
                var currentInfo = item2.Value;

                if (data1.TryGetValue(code, out var oldInfo))
                {
                    if (oldInfo.Price != currentInfo.Price)
                    {
                        results.Add(new CampaignComparisonResult
                        {
                            ProductCode = code,
                            ProductName = currentInfo.Name,
                            OldPrice = oldInfo.Price,
                            NewPrice = currentInfo.Price,
                            Status = currentInfo.Price > oldInfo.Price ? "Artış" : "Azalış"
                        });
                    }
                }
                else
                {
                    results.Add(new CampaignComparisonResult
                    {
                        ProductCode = code,
                        ProductName = currentInfo.Name,
                        OldPrice = 0,
                        NewPrice = currentInfo.Price,
                        Status = "Yeni Ürün"
                    });
                }
            }

            foreach (var item1 in data1)
            {
                if (!data2.ContainsKey(item1.Key))
                {
                    results.Add(new CampaignComparisonResult
                    {
                        ProductCode = item1.Key,
                        ProductName = item1.Value.Name,
                        OldPrice = item1.Value.Price,
                        NewPrice = 0,
                        Status = "Kaldırıldı"
                    });
                }
            }

            return results.OrderByDescending(r => Math.Abs(r.Difference)).ToList();
        }

        private Dictionary<string, (string Name, decimal Price)> ReadExcelData(string filePath, string sheetName, string codeCol, string nameCol, string priceCol)
        {
            var data = new Dictionary<string, (string Name, decimal Price)>();
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null) return data;

                int rows = ws.Dimension?.Rows ?? 0;
                int cols = ws.Dimension?.Columns ?? 0;

                int codeIdx = -1, nameIdx = -1, priceIdx = -1;
                for (int c = 1; c <= cols; c++)
                {
                    string h = ws.Cells[1, c].Text;
                    if (h == codeCol) codeIdx = c;
                    if (h == nameCol) nameIdx = c;
                    if (h == priceCol) priceIdx = c;
                }

                if (codeIdx == -1 || priceIdx == -1) return data;

                for (int r = 2; r <= rows; r++)
                {
                    string code = ws.Cells[r, codeIdx].Text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(code)) continue;

                    string name = nameIdx != -1 ? ws.Cells[r, nameIdx].Text?.Trim() ?? "" : "";
                    
                    decimal price = 0;
                    var val = ws.Cells[r, priceIdx].Value;
                    if (val is double d) price = (decimal)d;
                    else if (val is decimal m) price = m;
                    else decimal.TryParse(ws.Cells[r, priceIdx].Text.Replace("₺", "").Trim(), out price);

                    data[code] = (name, price);
                }
            }
            return data;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var results = GridResults.ItemsSource as List<CampaignComparisonResult>;
            if (results == null || !results.Any()) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = $"Karsilastirma_Sonucu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var package = new ExcelPackage())
                    {
                        var ws = package.Workbook.Worksheets.Add("Sonuçlar");
                        ws.Cells[1, 1].Value = "Ürün Kodu";
                        ws.Cells[1, 2].Value = "Ürün Adı";
                        ws.Cells[1, 3].Value = "Eski Fiyat";
                        ws.Cells[1, 4].Value = "Yeni Fiyat";
                        ws.Cells[1, 5].Value = "Fark";
                        ws.Cells[1, 6].Value = "Değişim %";
                        ws.Cells[1, 7].Value = "Durum";

                        using (var range = ws.Cells[1, 1, 1, 7])
                        {
                            range.Style.Font.Bold = true;
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(124, 58, 237));
                            range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        }

                        int row = 2;
                        foreach (var res in results)
                        {
                            ws.Cells[row, 1].Value = res.ProductCode;
                            ws.Cells[row, 2].Value = res.ProductName;
                            ws.Cells[row, 3].Value = res.OldPrice;
                            ws.Cells[row, 4].Value = res.NewPrice;
                            ws.Cells[row, 5].Value = res.Difference;
                            ws.Cells[row, 6].Value = res.DifferencePercentage / 100;
                            ws.Cells[row, 6].Style.Numberformat.Format = "0.0%";
                            ws.Cells[row, 7].Value = res.Status;
                            row++;
                        }

                        ws.Cells.AutoFitColumns();
                        package.SaveAs(new FileInfo(saveFileDialog.FileName));
                    }
                    ViewSnackbar.MessageQueue?.Enqueue("Excel başarıyla dışa aktarıldı.");
                }
                catch (Exception ex)
                {
                    ViewSnackbar.MessageQueue?.Enqueue("Hata: " + ex.Message);
                }
            }
        }
    }

    public class CampaignComparisonResult
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal Difference => NewPrice - OldPrice;
        public double DifferencePercentage => OldPrice != 0 ? (double)((NewPrice - OldPrice) / OldPrice) * 100 : (OldPrice == 0 && NewPrice > 0 ? 100 : 0);
        public string Status { get; set; } = "";
    }
}
