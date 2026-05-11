using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OfficeOpenXml;
using System.Linq;
using ArcelikApp.Data;
using ArcelikApp.Models;
using ArcelikApp.Excel.Mapping;
using ArcelikApp.Excel.Processors;
using System.IO;
using System.Threading.Tasks;
using System;
using ArcelikApp.Services;


namespace ArcelikExcelApp.Views
{
    public partial class ExcelIslemleriView : UserControl
    {
        private string _selectedFilePath = string.Empty;

        public ExcelIslemleriView()
        {
            InitializeComponent();
            ExcelPackage.License.SetNonCommercialPersonal("NART");
            MainSnackbar.MessageQueue = new MaterialDesignThemes.Wpf.SnackbarMessageQueue(TimeSpan.FromSeconds(3));

            // Beyaz Eşya alt tip ComboBox'ını Registry'den doldur
            foreach (var typeName in ColumnMappingRegistry.GetAllFileTypeNames())
                CmbWhiteGoodsType.Items.Add(typeName);

            if (CmbWhiteGoodsType.Items.Count > 0)
                CmbWhiteGoodsType.SelectedIndex = 0;

            // Dönem bilgilerini doldur
            InitializePeriodControls();
        }

        private void InitializePeriodControls()
        {
            // Yılları doldur (Geçen yıl, bu yıl, gelecek yıl)
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear - 1; y <= currentYear + 1; y++)
                CmbPeriodYear.Items.Add(y);
            CmbPeriodYear.SelectedItem = currentYear;

            // Ayı seç (1-indexed tag kullanıyoruz)
            int currentMonth = DateTime.Now.Month;
            CmbPeriodMonth.SelectedIndex = currentMonth - 1;
        }


        // ─── Dosya Seçimi ─────────────────────────────────────────────────────────────
        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Dosyaları|*.xlsx;*.xlsm;*.xls",
                Title  = "Excel Dosyası Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                TxtSelectedFile.Text = Path.GetFileName(_selectedFilePath);
                LoadWorksheetsIfOliz();
            }
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _selectedFilePath = files[0];
                    TxtSelectedFile.Text = Path.GetFileName(_selectedFilePath);
                    LoadWorksheetsIfOliz();
                }
            }
        }

        // ─── Kategori Değişince Panel Görünürlüklerini Ayarla ─────────────────────────
        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = CmbCategory.SelectedItem as ComboBoxItem;
            string category  = selectedItem?.Content?.ToString() ?? "";

            CmbWorksheet.Visibility = (category == "Oliz Kampanya" || category == "Oliz Paket Kampanya")
                ? Visibility.Visible : Visibility.Collapsed;
            
            // Beyaz Eşya veya Kea seçilirse Tip panelini göster
            bool showTypePanel = (category == "Beyaz Eşya" || category == "Kea");
            CmbWhiteGoodsType.Visibility = showTypePanel ? Visibility.Visible : Visibility.Collapsed;

            // Oliz Kampanya seçilirse Merge checkbox'ını ve Manuel Ekle butonunu göster
            ChkMergeOliz.Visibility = (category == "Oliz Kampanya") ? Visibility.Visible : Visibility.Collapsed;
            BtnManualOliz.Visibility = (category == "Oliz Kampanya") ? Visibility.Visible : Visibility.Collapsed;


            if (showTypePanel)
            {
                CmbWhiteGoodsType.Items.Clear();
                foreach (var typeName in ColumnMappingRegistry.GetTypesByCategory(category))
                    CmbWhiteGoodsType.Items.Add(typeName);

                if (CmbWhiteGoodsType.Items.Count > 0)
                    CmbWhiteGoodsType.SelectedIndex = 0;
            }

            LoadWorksheetsIfOliz();
        }

        // ─── Oliz için Çalışma Sayfalarını Yükle ──────────────────────────────────────
        private async void LoadWorksheetsIfOliz()
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath)) return;

            var selectedItem = CmbCategory.SelectedItem as ComboBoxItem;
            string? cat = selectedItem?.Content?.ToString();
            if (cat != "Oliz Kampanya") return;

            CmbWorksheet.Items.Clear();

            if (FileHelper.IsFileLocked(_selectedFilePath))
            {
                MainSnackbar.MessageQueue?.Enqueue("⚠️ Excel dosyası açık olduğu için sayfalar okunamadı.");
                return;
            }

            try
            {
                using var package = new ExcelPackage(new FileInfo(_selectedFilePath));
                foreach (var worksheet in package.Workbook.Worksheets)
                    CmbWorksheet.Items.Add(worksheet.Name);

                if (CmbWorksheet.Items.Count > 0)
                    CmbWorksheet.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Excel Hatası", $"Excel sayfaları yüklenirken hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
        }

        // ─── Yükle ve İşle ────────────────────────────────────────────────────────────
        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen önce bir dosya seçin.");
                return;
            }

            if (CmbCategory.SelectedItem == null)
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen bir kategori seçin.");
                return;
            }

            var categoryItem = CmbCategory.SelectedItem as ComboBoxItem;
            string categoryName = categoryItem?.Content?.ToString() ?? "";

            // Oliz: çalışma sayfası seçili mi?
            string worksheetName = "";
            if (categoryName == "Oliz Kampanya")
            {
                if (CmbWorksheet.SelectedItem == null)
                {
                    MainSnackbar.MessageQueue?.Enqueue("Lütfen bir çalışma sayfası seçin.");
                    return;
                }
                worksheetName = CmbWorksheet.SelectedItem.ToString()!;
            }

            string whiteGoodsType = "";
            if (categoryName == "Beyaz Eşya" || categoryName == "Kea")
            {
                if (CmbWhiteGoodsType.SelectedItem == null)
                {
                    MainSnackbar.MessageQueue?.Enqueue("Lütfen bir ürün tipi seçin.");
                    return;
                }
                whiteGoodsType = CmbWhiteGoodsType.SelectedItem.ToString()!;
            }


            // Dönem bilgisini al
            int selectedMonth = 1;
            if (CmbPeriodMonth.SelectedItem is ComboBoxItem monthItem)
                selectedMonth = int.Parse(monthItem.Tag.ToString()!);
            
            int selectedYear = (int)CmbPeriodYear.SelectedItem;
            bool isHistoryMode = ChkIsHistory.IsChecked ?? false;
            bool mergeOliz = ChkMergeOliz.IsChecked ?? false;

            // Uzantı kontrolü (.xlsx)

            string extension = System.IO.Path.GetExtension(_selectedFilePath).ToLower();
            if (extension != ".xlsx")
            {
                await ModernDialogService.ShowAsync("Geçersiz Dosya", "Sadece .xlsx uzantılı dosyalar desteklenmektedir.", ModernDialogType.Warning);
                return;
            }

            // Geçmiş fiyat modunda değilse dosya adı çakışma kontrolü yap
            if (!isHistoryMode)
            {
                string fileName = System.IO.Path.GetFileName(_selectedFilePath);
                using (var context = new ArcelikApp.Data.AppDbContext())
                {
                    if (context.UploadedFiles.Any(f => f.FileName == fileName))
                    {
                        await ModernDialogService.ShowAsync("Dosya Zaten Mevcut", $"'{fileName}' isimli bir dosya zaten yüklü. Lütfen farklı bir isimle deneyin veya mevcut dosyayı Dosya Yönetimi panelinden silin.", ModernDialogType.Warning);
                        return;
                    }
                }
            }

            // Geçmiş fiyat modunda, aynı ay/yıl/tip kombinasyonu zaten mevcut mu kontrol et
            if (isHistoryMode && (categoryName == "Beyaz Eşya" || categoryName == "Kea"))
            {
                using (var context = new ArcelikApp.Data.AppDbContext())
                {
                    bool alreadyExists = false;
                    if (categoryName == "Beyaz Eşya")
                    {
                        alreadyExists = context.HistoricalWhiteGoodsProducts
                            .Any(h => h.ExcelFileType == whiteGoodsType
                                   && h.PeriodMonth == selectedMonth
                                   && h.PeriodYear == selectedYear);
                    }
                    else if (categoryName == "Kea")
                    {
                        alreadyExists = context.HistoricalKeaProducts
                            .Any(h => h.ExcelFileType == whiteGoodsType
                                   && h.PeriodMonth == selectedMonth
                                   && h.PeriodYear == selectedYear);
                    }

                    if (alreadyExists)
                    {
                        string monthName = ((ComboBoxItem)CmbPeriodMonth.SelectedItem).Content.ToString();
                        await ModernDialogService.ShowAsync(
                            "Kayıt Zaten Mevcut",
                            $"'{whiteGoodsType}' için {monthName} {selectedYear} dönemine ait geçmiş fiyat kaydı zaten mevcut. Aynı dönem tekrar eklenemez.",
                            ModernDialogType.Warning);
                        return;
                    }
                }
            }

            // Dosya kilitli mi kontrolü (Excel'de açıksa hata verir)
            if (FileHelper.IsFileLocked(_selectedFilePath))
            {
                MainSnackbar.MessageQueue?.Enqueue("⚠️ Seçilen Excel dosyası şu an açık! Lütfen dosyayı kapatıp tekrar deneyin.");
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            BtnUpload.IsEnabled       = false;

            int importedCount = 0;

            try
            {
                await Task.Run(() =>
                {
                    using var context = new AppDbContext();

                    int fileId;

                    if (isHistoryMode)
                    {
                        // Geçmiş fiyat modunda dosyayı sisteme KAYDETME — sadece işle
                        fileId = 0; // dosya referansı yok

                        if (categoryName == "Beyaz Eşya")
                            importedCount = ProcessWhiteGoodsExcel(context, fileId, whiteGoodsType, selectedMonth, selectedYear, isHistoryOnly: true);
                        else if (categoryName == "Kea")
                            importedCount = ProcessKeaExcel(context, fileId, whiteGoodsType, selectedMonth, selectedYear, isHistoryOnly: true);
                    }
                    else
                    {
                        // Normal mod: dosyayı uygulama içine kopyala ve kaydet
                        string relativePath = FileHelper.CopyToStorage(_selectedFilePath);

                        var newFile = new UploadedFile
                        {
                            FileName   = Path.GetFileName(_selectedFilePath),
                            FilePath   = relativePath,
                            FileData   = File.ReadAllBytes(_selectedFilePath),
                            Category   = categoryName,
                            UploadDate = DateTime.Now.ToString("dd.MM.yyyy")
                        };
                        context.UploadedFiles.Add(newFile);
                        context.SaveChanges();
                        fileId = newFile.Id;

                        // İşleme için artık kopyalanan dosyayı kullanabiliriz
                        string absoluteStoragePath = FileHelper.GetAbsolutePath(relativePath);

                        string originalPath = _selectedFilePath;
                        _selectedFilePath = absoluteStoragePath;

                        if (categoryName == "Oliz Kampanya")
                            ProcessOlizExcel(context, fileId, worksheetName, mergeOliz);
                        else if (categoryName == "Beyaz Eşya")
                            importedCount = ProcessWhiteGoodsExcel(context, fileId, whiteGoodsType, selectedMonth, selectedYear, isHistoryOnly: false);
                        else if (categoryName == "Kea")
                            importedCount = ProcessKeaExcel(context, fileId, whiteGoodsType, selectedMonth, selectedYear, isHistoryOnly: false);

                        _selectedFilePath = originalPath;
                    }
                });

                if (categoryName == "Beyaz Eşya" || categoryName == "Kea")
                    await ModernDialogService.ShowAsync("Başarılı", $"✅ {whiteGoodsType}: {importedCount} ürün başarıyla kaydedildi.\n\nLütfen değişikliklerin yansıması için Maliyet Hesaplama ekranından listeyi yeniden hesaplatıp kaydedin.", ModernDialogType.Success);
                else if (categoryName == "Oliz Kampanya")
                    await ModernDialogService.ShowAsync("Başarılı", "✅ Oliz Kampanya verileri başarıyla yüklendi.\n\nLütfen değişikliklerin yansıması için Maliyet Hesaplama ekranından listeyi yeniden hesaplatıp kaydedin.", ModernDialogType.Success);
                else if (categoryName == "Oliz Paket Kampanya")
                    await ModernDialogService.ShowAsync("Başarılı", $"✅ {importedCount} paket kampanya başarıyla yüklendi.\n\nLütfen değişikliklerin yansıması için Maliyet Hesaplama ekranından listeyi yeniden hesaplatıp kaydedin.", ModernDialogType.Success);
                else
                    MainSnackbar.MessageQueue?.Enqueue($"{categoryName} dosyası yüklendi.");
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("İşlem Hatası", $"Yükleme sırasında bir hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnUpload.IsEnabled       = true;

                // Başarılı yüklemeden sonra formu temizle
                ResetFileSelection();
            }
        }

        // ─── Form Temizleme ────────────────────────────────────────────────────────────
        private void ResetFileSelection()
        {
            _selectedFilePath    = string.Empty;
            TxtSelectedFile.Text = "";
        }

        // ─── Beyaz Eşya Excel İşleme ──────────────────────────────────────────────────
        private int ProcessWhiteGoodsExcel(AppDbContext context, int fileId, string whiteGoodsType, int month, int year, bool isHistoryOnly)
        {
            // 1. Profile bul

            var profile = ColumnMappingRegistry.GetProfile(whiteGoodsType);
            if (profile == null)
                throw new InvalidOperationException($"'{whiteGoodsType}' için kolon profili bulunamadı.");

            using var package = new ExcelPackage(new FileInfo(_selectedFilePath));

            // Tüm sayfalarda arama yap — bazı dosyalarda veri ilk sayfada olmayabilir
            ExcelWorksheet? worksheet = null;
            foreach (var ws in package.Workbook.Worksheets)
            {
                // İlk boyutlu (veri içeren) sayfayı al
                if (ws.Dimension != null)
                {
                    worksheet = ws;
                    break;
                }
            }

            if (worksheet == null)
                throw new InvalidOperationException("Excel dosyasında veri içeren bir çalışma sayfası bulunamadı.");

            // DEBUG: Header satırındaki gerçek başlıkları yakala (0 kayıt gelirse tanılama için)
            var foundHeaders = new System.Text.StringBuilder();
            int colCount = worksheet.Dimension?.Columns ?? 0;
            for (int c = 1; c <= colCount; c++)
            {
                string h = worksheet.Cells[profile.HeaderRow, c].Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(h))
                    foundHeaders.Append($"[{c}:{h}] ");
            }
            string headersDebug = foundHeaders.ToString();

            var processor = new WhiteGoodsExcelProcessor();

            // 2. Processor'ı çalıştır
            var products = processor.Process(worksheet, profile, fileId, month, year);

            if (products.Count == 0)
            {
                // Hiç kayıt gelmedi — başlık eşleşmesi olmayabilir
                throw new InvalidOperationException(
                    $"Sayfa '{worksheet.Name}' için hiç kayıt okunamadı. " +
                    $"Excel başlıkları: {headersDebug}" );
            }

            // 3. Veritabanına kaydet
            if (isHistoryOnly)
            {
                // Sadece tarihçe tablosuna ekle
                var historyProducts = products.Select(p => MapToHistory(p, month, year)).ToList();
                context.HistoricalWhiteGoodsProducts.AddRange(historyProducts);
            }
            else
            {
                // Güncel liste olarak yükle. 
                // Önce içeride başka bir ayın verisi varsa onu arşivle.
                ArchiveExistingDataIfNewer(context, month, year, whiteGoodsType);

                // Bu kategori için mevcut güncel verileri sil (aynı ayın tekrar yüklenmesi durumu)
                var existingCurrent = context.WhiteGoodsProducts
                    .Where(p => p.ExcelFileType == whiteGoodsType && p.PeriodMonth == month && p.PeriodYear == year)
                    .ToList();
                if (existingCurrent.Any())
                    context.WhiteGoodsProducts.RemoveRange(existingCurrent);

                context.WhiteGoodsProducts.AddRange(products);
            }

            context.SaveChanges();

            return products.Count;
        }

        private void ArchiveExistingDataIfNewer(AppDbContext context, int newMonth, int newYear, string category)
        {
            // Mevcut güncel verileri bul
            var currentData = context.WhiteGoodsProducts
                .Where(p => p.ExcelFileType == category)
                .ToList();

            if (!currentData.Any()) return;

            var first = currentData.First();
            
            // Eğer veritabanındaki veri, yeni gelenden daha eskiyse arşivle
            bool isOlder = (first.PeriodYear < newYear) || (first.PeriodYear == newYear && first.PeriodMonth < newMonth);

            if (isOlder)
            {
                var historyEntries = currentData.Select(p => MapToHistory(p, p.PeriodMonth, p.PeriodYear)).ToList();
                context.HistoricalWhiteGoodsProducts.AddRange(historyEntries);
                
                // Arşivledikten sonra güncel tablodan bu kategoriyi temizle (çünkü yeni ayın verisi gelecek)
                context.WhiteGoodsProducts.RemoveRange(currentData);
                context.SaveChanges();
            }
        }

        private HistoricalWhiteGoodsProduct MapToHistory(WhiteGoodsProduct p, int month, int year)
        {
            return new HistoricalWhiteGoodsProduct
            {
                PeriodMonth = month,
                PeriodYear = year,
                ArchiveDate = DateTime.Now,
                ExcelFileType = p.ExcelFileType,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Description = p.Description,
                EnergyClass = p.EnergyClass,
                CashPrice = p.CashPrice,
                WholesalePrice30 = p.WholesalePrice30,
                WholesalePrice60 = p.WholesalePrice60,
                WholesalePrice90 = p.WholesalePrice90,
                WholesalePrice120 = p.WholesalePrice120,
                Installment2Down = p.Installment2Down,
                Installment2Total = p.Installment2Total,
                Installment4Down = p.Installment4Down,
                Installment4Total = p.Installment4Total,
                Installment8Down = p.Installment8Down,
                Installment8Total = p.Installment8Total,
                PromoCashPrice = p.PromoCashPrice,
                PromoInstall1x2 = p.PromoInstall1x2,
                PromoInstall1x4 = p.PromoInstall1x4,
                PromoInstall1x8 = p.PromoInstall1x8,
                UploadedFileId = p.UploadedFileId
            };
        }


        // ─── KEA Excel İşleme ─────────────────────────────────────────────────────────
        private int ProcessKeaExcel(AppDbContext context, int fileId, string keaType, int month, int year, bool isHistoryOnly)
        {
            var profile = ColumnMappingRegistry.GetProfile(keaType);
            if (profile == null)
                throw new InvalidOperationException($"'{keaType}' için kolon profili bulunamadı.");

            using var package = new ExcelPackage(new FileInfo(_selectedFilePath));
            ExcelWorksheet? worksheet = package.Workbook.Worksheets.FirstOrDefault(ws => ws.Dimension != null);

            if (worksheet == null)
                throw new InvalidOperationException("Excel dosyasında veri içeren bir çalışma sayfası bulunamadı.");

            var processor = new KeaExcelProcessor();
            var products = processor.Process(worksheet, profile, fileId, month, year);

            if (products.Count == 0)
                throw new InvalidOperationException($"'{worksheet.Name}' sayfasından veri okunamadı.");

            // 3. Veritabanına kaydet
            if (isHistoryOnly)
            {
                // Sadece tarihçe tablosuna ekle
                var historyProducts = products.Select(p => MapToKeaHistory(p, month, year)).ToList();
                context.HistoricalKeaProducts.AddRange(historyProducts);
            }
            else
            {
                // Güncel liste olarak yükle. 
                // Önce içeride başka bir ayın verisi varsa onu arşivle.
                ArchiveExistingKeaDataIfNewer(context, month, year, keaType);

                // Bu kategori için mevcut güncel verileri sil (aynı ayın tekrar yüklenmesi durumu)
                var existingCurrent = context.KeaProducts
                    .Where(p => p.ExcelFileType == keaType && p.PeriodMonth == month && p.PeriodYear == year)
                    .ToList();
                if (existingCurrent.Any())
                    context.KeaProducts.RemoveRange(existingCurrent);

                context.KeaProducts.AddRange(products);
            }

            context.SaveChanges();

            return products.Count;
        }

        private void ArchiveExistingKeaDataIfNewer(AppDbContext context, int newMonth, int newYear, string category)
        {
            // Mevcut güncel verileri bul
            var currentData = context.KeaProducts
                .Where(p => p.ExcelFileType == category)
                .ToList();

            if (!currentData.Any()) return;

            var first = currentData.First();

            // Eğer veritabanındaki veri, yeni gelenden daha eskiyse arşivle
            bool isOlder = (first.PeriodYear < newYear) || (first.PeriodYear == newYear && first.PeriodMonth < newMonth);

            if (isOlder)
            {
                var historyEntries = currentData.Select(p => MapToKeaHistory(p, p.PeriodMonth, p.PeriodYear)).ToList();
                context.HistoricalKeaProducts.AddRange(historyEntries);

                // Arşivledikten sonra güncel tablodan bu kategoriyi temizle (çünkü yeni ayın verisi gelecek)
                context.KeaProducts.RemoveRange(currentData);
                context.SaveChanges();
            }
        }

        private HistoricalKeaProduct MapToKeaHistory(KeaProduct p, int month, int year)
        {
            return new HistoricalKeaProduct
            {
                PeriodMonth = month,
                PeriodYear = year,
                ArchiveDate = DateTime.Now,
                ExcelFileType = p.ExcelFileType,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Description = p.Description,
                CashPrice = p.CashPrice,
                WholesalePrice30 = p.WholesalePrice30,
                WholesalePrice60 = p.WholesalePrice60,
                WholesalePrice90 = p.WholesalePrice90,
                WholesalePrice120 = p.WholesalePrice120,
                PromoCashPrice = p.PromoCashPrice,
                PromoInstall1x2 = p.PromoInstall1x2,
                PromoInstall1x4 = p.PromoInstall1x4,
                PromoInstall1x8 = p.PromoInstall1x8,
                UploadedFileId = p.UploadedFileId
            };
        }


        // ─── Oliz Kampanya Excel İşleme ───────────────────────────────────────────────
        private void ProcessOlizExcel(AppDbContext context, int fileId, string sheetName, bool mergeWithPrevious)
        {
            var mergedCampaigns = new Dictionary<string, OlizCampaign>();

            if (mergeWithPrevious)
            {
                // Önceki en güncel Oliz dosyasını bul (kendisi hariç)
                var previousFile = context.UploadedFiles
                    .Where(f => f.Category == "Oliz Kampanya" && f.Id < fileId)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefault();

                if (previousFile != null)
                {
                    var oldCampaigns = context.OlizCampaigns.Where(c => c.UploadedFileId == previousFile.Id).ToList();
                    foreach (var c in oldCampaigns)
                    {
                        var copy = new OlizCampaign
                        {
                            Brand = c.Brand,
                            ProductGroup = c.ProductGroup,
                            ProductCode = c.ProductCode,
                            ProductDescription = c.ProductDescription,
                            DiscountAmount = c.DiscountAmount,
                            DiscountNetAmount = c.DiscountNetAmount,
                            CampaignStartDate = c.CampaignStartDate,
                            CampaignEndDate = c.CampaignEndDate,
                            LastTransportDate = c.LastTransportDate,
                            LastBarcodeScanDate = c.LastBarcodeScanDate,
                            CampaignCode = c.CampaignCode,
                            CampaignShortDescription = c.CampaignShortDescription,
                            GeneralDescription = c.GeneralDescription,
                            UploadedFileId = fileId // Yeni dosya ID'sine bağlıyoruz
                        };

                        string key = c.ProductCode?.Trim().ToUpperInvariant() ?? string.Empty;
                        if (!string.IsNullOrEmpty(key))
                            mergedCampaigns[key] = copy;
                    }
                }
            }

            using var package = new ExcelPackage(new FileInfo(_selectedFilePath));
            var worksheet     = package.Workbook.Worksheets[sheetName];
            if (worksheet == null) return;

            int rowCount = worksheet.Dimension?.Rows ?? 0;

            for (int row = 2; row <= rowCount; row++)
            {
                var campaign = new OlizCampaign
                {
                    Brand                  = worksheet.Cells[row, 1].Text,
                    ProductGroup           = worksheet.Cells[row, 2].Text,
                    ProductCode            = worksheet.Cells[row, 3].Text,
                    ProductDescription     = worksheet.Cells[row, 4].Text,
                    DiscountAmount         = ParseDecimalFromCell(worksheet.Cells[row, 5]),
                    DiscountNetAmount      = ParseDecimalFromCell(worksheet.Cells[row, 6]),
                    CampaignStartDate      = ParseDateFromCell(worksheet.Cells[row, 7]),
                    CampaignEndDate        = ParseDateFromCell(worksheet.Cells[row, 8]),
                    LastTransportDate      = ParseDateFromCell(worksheet.Cells[row, 9]),
                    LastBarcodeScanDate    = ParseDateFromCell(worksheet.Cells[row, 10]),
                    CampaignCode           = worksheet.Cells[row, 11].Text,
                    CampaignShortDescription = worksheet.Cells[row, 12].Text,
                    GeneralDescription     = worksheet.Cells[row, 13].Text,
                    UploadedFileId         = fileId
                };

                if (string.IsNullOrWhiteSpace(campaign.ProductCode) &&
                    string.IsNullOrWhiteSpace(campaign.CampaignCode)) continue;

                string key = campaign.ProductCode?.Trim().ToUpperInvariant() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    // Dictionary'de varsa üzerine yazar (günceller), yoksa yeni ekler
                    mergedCampaigns[key] = campaign;
                }
                else
                {
                    // Ürün kodu yok ama kampanya kodu var (nadiren olabilir)
                    // Benzersiz bir key uydurup dictionary'ye atalım ki eklensin
                    mergedCampaigns[Guid.NewGuid().ToString()] = campaign;
                }
            }

            context.OlizCampaigns.AddRange(mergedCampaigns.Values);
            context.SaveChanges();
        }

        // ─── Manuel Oliz Kampanyası Ekleme ─────────────────────────────────────────
        private void BtnOpenManualOliz_Click(object sender, RoutedEventArgs e)
        {
            TxtManualOlizCode.Text = "";
            TxtManualOlizDiscount.Text = "";
            ManualOlizOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCancelManualOliz_Click(object sender, RoutedEventArgs e)
        {
            ManualOlizOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnSaveManualOliz_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtManualOlizCode.Text.Trim();
            string discountStr = TxtManualOlizDiscount.Text.Trim();

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(discountStr))
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen ürün kodu ve indirim tutarını girin.");
                return;
            }

            if (!decimal.TryParse(discountStr.Replace(".", ","), out decimal discount))
            {
                MainSnackbar.MessageQueue?.Enqueue("Geçersiz indirim tutarı formatı.");
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    using var context = new AppDbContext();
                    var lastFile = context.UploadedFiles
                        .Where(f => f.Category == "Oliz Kampanya")
                        .OrderByDescending(f => f.Id)
                        .FirstOrDefault();

                    if (lastFile == null)
                    {
                        throw new InvalidOperationException("Sistemde önceden yüklenmiş bir Oliz Kampanya dosyası bulunamadı. Lütfen önce bir Oliz Excel'i yükleyin.");
                    }

                    var existing = context.OlizCampaigns.FirstOrDefault(c => c.UploadedFileId == lastFile.Id && c.ProductCode == code);
                    if (existing != null)
                    {
                        existing.DiscountNetAmount = discount;
                        existing.CampaignShortDescription = "Manuel Güncellendi";
                    }
                    else
                    {
                        context.OlizCampaigns.Add(new OlizCampaign
                        {
                            ProductCode = code,
                            DiscountNetAmount = discount,
                            DiscountAmount = discount,
                            UploadedFileId = lastFile.Id,
                            CampaignShortDescription = "Manuel Eklendi",
                            Brand = "",
                            ProductGroup = "",
                            ProductDescription = "Manuel Eklenen Kampanya",
                            CampaignStartDate = DateTime.Now.ToString("dd.MM.yyyy"),
                            CampaignEndDate = "31.12.2099",
                            LastTransportDate = "",
                            LastBarcodeScanDate = "",
                            CampaignCode = "MANUAL",
                            GeneralDescription = ""
                        });
                    }
                    context.SaveChanges();
                });

                ManualOlizOverlay.Visibility = Visibility.Collapsed;
                await ModernDialogService.ShowAsync("Başarılı", $"{code} kodlu ürün için {discount} ₺ indirim başarıyla en son Oliz listesine eklendi/güncellendi.\nMaliyet Hesaplama ekranından tekrar hesaplatıp kaydedebilirsiniz.", ModernDialogType.Success);
            }
            catch (InvalidOperationException ex)
            {
                await ModernDialogService.ShowAsync("Uyarı", ex.Message, ModernDialogType.Warning);
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata", $"Kayıt sırasında hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
        }


        // ─── Yardımcı Parser'lar (Oliz için) ──────────────────────────────────────────
        private decimal ParseDecimalFromCell(ExcelRange cell)
        {
            if (cell.Value == null) return 0;
            if (cell.Value is double d)  return (decimal)d;
            if (cell.Value is decimal m) return m;
            if (cell.Value is int i)     return (decimal)i;

            string text = (cell.Text ?? string.Empty)
                .ToUpper().Replace("TL", "").Replace(" ", "").Trim();

            return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                new System.Globalization.CultureInfo("tr-TR"), out decimal result)
                ? result : 0;
        }

        private string ParseDateFromCell(ExcelRange cell)
        {
            if (cell.Value == null) return string.Empty;

            DateTime parsedDt = DateTime.MinValue;

            if (cell.Value is DateTime dt)        parsedDt = dt;
            else if (cell.Value is double dbl)    parsedDt = DateTime.FromOADate(dbl);
            else
            {
                string text = cell.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    DateTime.TryParse(text, new System.Globalization.CultureInfo("tr-TR"),
                        System.Globalization.DateTimeStyles.None, out parsedDt);
            }

            return parsedDt == DateTime.MinValue ? string.Empty : parsedDt.ToString("dd.MM.yyyy");
        }
    }
}
