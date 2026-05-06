using ArcelikApp.Data;
using ArcelikApp.Models;
using ArcelikApp.Services;
using ArcelikApp.Excel.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcelikExcelApp.Views
{
    public partial class TarihceView : UserControl
    {
        private List<HistoricalDisplayItem> _allData = new();
        private List<HistoricalDisplayItem> _filteredData = new();
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;

        public class HistoricalDisplayItem
        {
            public int Id { get; set; }
            public int PeriodMonth { get; set; }
            public int PeriodYear { get; set; }
            public DateTime ArchiveDate { get; set; }
            public string ExcelFileType { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public decimal? CashPrice { get; set; }
            public decimal? WholesalePrice30 { get; set; }
            public decimal? WholesalePrice60 { get; set; }
            public decimal? WholesalePrice90 { get; set; }
            public decimal? WholesalePrice120 { get; set; }
            public string PeriodDisplay => $"{PeriodMonth}/{PeriodYear}";
        }

        public TarihceView()
        {
            InitializeComponent();
        }

        public TarihceView(string initialProductGroup)
        {
            InitializeComponent();
            
            // ComboBox henüz Loaded olmadığı için Tag üzerinden eşleşme yapacağız
            foreach (ComboBoxItem item in CmbProductGroup.Items)
            {
                if (item.Tag?.ToString() == initialProductGroup)
                {
                    CmbProductGroup.SelectedItem = item;
                    break;
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeFilters();
            _ = LoadDataAsync();
        }

        private void InitializeFilters()
        {
            // Yıllar
            int currentYear = DateTime.Now.Year;
            for (int y = currentYear; y >= currentYear - 3; y--)
                CmbYear.Items.Add(y);
            CmbYear.SelectedIndex = 0;

            // Ay
            CmbMonth.SelectedIndex = DateTime.Now.Month - 1;

            UpdateCategoryFilter();
        }

        private void UpdateCategoryFilter()
        {
            if (CmbCategory == null) return;

            string productGroup = (CmbProductGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BeyazEsya";
            
            CmbCategory.SelectionChanged -= Filter_Changed;
            CmbCategory.Items.Clear();
            CmbCategory.Items.Add("Tümü");

            if (productGroup == "BeyazEsya")
            {
                foreach (var cat in ValorSettingsService.WhiteGoodsCategories)
                    CmbCategory.Items.Add(cat);
            }
            else
            {
                foreach (var cat in ColumnMappingRegistry.GetTypesByCategory("Kea"))
                    CmbCategory.Items.Add(cat);
            }

            CmbCategory.SelectedIndex = 0;
            CmbCategory.SelectionChanged += Filter_Changed;
        }

        private async Task LoadDataAsync()
        {
            if (!IsLoaded) return;

            try
            {
                OverlayLoading.Visibility = Visibility.Visible;

                string productGroup = (CmbProductGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BeyazEsya";
                int selectedMonth = 1;
                if (CmbMonth.SelectedItem is ComboBoxItem monthItem)
                    selectedMonth = int.Parse(monthItem.Tag.ToString()!);
                
                int selectedYear = (int)(CmbYear.SelectedItem ?? DateTime.Now.Year);
                string selectedCategory = CmbCategory.SelectedItem?.ToString() ?? "Tümü";

                _allData = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    
                    if (productGroup == "BeyazEsya")
                    {
                        var query = db.HistoricalWhiteGoodsProducts.AsQueryable();
                        if (selectedCategory != "Tümü")
                            query = query.Where(p => p.ExcelFileType == selectedCategory);

                        return query
                            .Where(p => p.PeriodMonth == selectedMonth && p.PeriodYear == selectedYear)
                            .OrderByDescending(p => p.ArchiveDate)
                            .Select(p => new HistoricalDisplayItem
                            {
                                Id = p.Id,
                                PeriodMonth = p.PeriodMonth,
                                PeriodYear = p.PeriodYear,
                                ArchiveDate = p.ArchiveDate,
                                ExcelFileType = p.ExcelFileType,
                                ProductCode = p.ProductCode,
                                ProductName = p.ProductName,
                                CashPrice = p.CashPrice,
                                WholesalePrice30 = p.WholesalePrice30,
                                WholesalePrice60 = p.WholesalePrice60,
                                WholesalePrice90 = p.WholesalePrice90,
                                WholesalePrice120 = p.WholesalePrice120
                            })
                            .ToList();
                    }
                    else
                    {
                        var query = db.HistoricalKeaProducts.AsQueryable();
                        if (selectedCategory != "Tümü")
                            query = query.Where(p => p.ExcelFileType == selectedCategory);

                        return query
                            .Where(p => p.PeriodMonth == selectedMonth && p.PeriodYear == selectedYear)
                            .OrderByDescending(p => p.ArchiveDate)
                            .Select(p => new HistoricalDisplayItem
                            {
                                Id = p.Id,
                                PeriodMonth = p.PeriodMonth,
                                PeriodYear = p.PeriodYear,
                                ArchiveDate = p.ArchiveDate,
                                ExcelFileType = p.ExcelFileType,
                                ProductCode = p.ProductCode,
                                ProductName = p.ProductName,
                                CashPrice = p.CashPrice,
                                WholesalePrice30 = p.WholesalePrice30,
                                WholesalePrice60 = p.WholesalePrice60,
                                WholesalePrice90 = p.WholesalePrice90,
                                WholesalePrice120 = p.WholesalePrice120
                            })
                            .ToList();
                    }
                });

                FilterData();
            }
            catch (Exception ex)
            {
                _ = ModernDialogService.ShowAsync("Hata", $"Veriler yüklenirken hata oluştu: {ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                OverlayLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void FilterData()
        {
            string search = TxtSearch.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(search))
            {
                _filteredData = _allData;
            }
            else
            {
                _filteredData = _allData.Where(p => 
                    p.ProductCode.ToLowerInvariant().Contains(search) || 
                    p.ProductName.ToLowerInvariant().Contains(search)).ToList();
            }

            TxtTotalCount.Text = $"{_filteredData.Count} Ürün";
            PnlNoData.Visibility = _filteredData.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            _totalPages = (int)Math.Ceiling((double)_filteredData.Count / _pageSize);
            if (_totalPages == 0) _totalPages = 1;
            _currentPage = 1;
            UpdateGrid();
        }

        private void UpdateGrid()
        {
            var pagedData = _filteredData
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            GridHistory.ItemsSource = pagedData;
            TxtPageInfo.Text = $"Sayfa {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < _totalPages;
        }

        private void ProductGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateCategoryFilter();
                _ = LoadDataAsync();
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) _ = LoadDataAsync();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterData();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; UpdateGrid(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; UpdateGrid(); }
        }
    }
}
