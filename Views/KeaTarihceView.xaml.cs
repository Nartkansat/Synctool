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
    public partial class KeaTarihceView : UserControl
    {
        private List<HistoricalKeaProduct> _allData = new();
        private List<HistoricalKeaProduct> _filteredData = new();
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;

        public class HistoricalDisplayItem : HistoricalKeaProduct
        {
            public string PeriodDisplay => $"{PeriodMonth}/{PeriodYear}";
        }

        public KeaTarihceView()
        {
            InitializeComponent();
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
            for (int y = currentYear; y >= currentYear - 2; y--)
                CmbYear.Items.Add(y);
            CmbYear.SelectedIndex = 0;

            // Ay
            CmbMonth.SelectedIndex = DateTime.Now.Month - 1;

            // Kategoriler
            CmbCategory.Items.Add("Tümü");
            foreach (var cat in ColumnMappingRegistry.GetTypesByCategory("Kea"))
                CmbCategory.Items.Add(cat);
            CmbCategory.SelectedIndex = 0;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                OverlayLoading.Visibility = Visibility.Visible;

                int selectedMonth = 1;
                if (CmbMonth.SelectedItem is ComboBoxItem monthItem)
                    selectedMonth = int.Parse(monthItem.Tag.ToString()!);
                
                int selectedYear = (int)(CmbYear.SelectedItem ?? DateTime.Now.Year);
                string selectedCategory = CmbCategory.SelectedItem?.ToString() ?? "Tümü";

                _allData = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var query = db.HistoricalKeaProducts.AsQueryable();

                    if (selectedCategory != "Tümü")
                        query = query.Where(p => p.ExcelFileType == selectedCategory);

                    return query
                        .Where(p => p.PeriodMonth == selectedMonth && p.PeriodYear == selectedYear)
                        .OrderByDescending(p => p.ArchiveDate)
                        .ToList();
                });

                FilterData();
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata", $"Veriler yüklenirken hata oluştu: {ex.Message}", ModernDialogType.Error);
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
                .Select(p => new HistoricalDisplayItem
                {
                    PeriodMonth = p.PeriodMonth,
                    PeriodYear = p.PeriodYear,
                    ArchiveDate = p.ArchiveDate,
                    ProductCode = p.ProductCode,
                    ProductName = p.ProductName,
                    CashPrice = p.CashPrice,
                    WholesalePrice30 = p.WholesalePrice30,
                    WholesalePrice60 = p.WholesalePrice60,
                    WholesalePrice90 = p.WholesalePrice90,
                    WholesalePrice120 = p.WholesalePrice120,
                    ExcelFileType = p.ExcelFileType
                })
                .ToList();

            GridHistory.ItemsSource = pagedData;
            TxtPageInfo.Text = $"Sayfa {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < _totalPages;
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
