using Synctool.Data;
using Synctool.DTOs;
using Synctool.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Synctool.Views
{
    public partial class KampanyaBilgiView : UserControl
    {
        private int _currentPage = 1;
        private const int _pageSize = 10;
        private bool _isLastPage = false;
        private bool _isLoading = false;
        private List<ManualCampaignInfoDto> _campaigns = new();

        private List<Border> _matchingBorders = new();
        private int _currentMatchIndex = -1;

        public KampanyaBilgiView()
        {
            InitializeComponent();
            MainSnackbar.MessageQueue = new MaterialDesignThemes.Wpf.SnackbarMessageQueue(TimeSpan.FromSeconds(3));
            _ = LoadCampaignsAsync(true);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadCampaignsAsync(true);
        }

        private void SvCampaigns_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Scroll en alta yaklaştığında (son 100px) yeni sayfa yükle
            if (!_isLastPage && !_isLoading && e.VerticalOffset > 0)
            {
                if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 100)
                {
                    _ = LoadCampaignsAsync(false);
                }
            }
        }

        private async Task LoadCampaignsAsync(bool isReset)
        {
            if (_isLoading) return;

            try
            {
                _isLoading = true;
                
                if (isReset)
                {
                    _currentPage = 1;
                    _isLastPage = false;
                    _campaigns.Clear();
                    
                    if (TxtSearch != null) TxtSearch.Text = string.Empty;
                    _matchingBorders.Clear();
                    _currentMatchIndex = -1;

                    LoadingOverlay.Visibility = Visibility.Visible;
                    EmptyState.Visibility = Visibility.Collapsed;
                    IcCampaigns.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PagingLoading.Visibility = Visibility.Visible;
                }

                // Query local MySQL database using EF Core
                var result = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    
                    var campaigns = db.ManualCampaigns
                        .Include(c => c.Products)
                        .OrderByDescending(c => c.CreatedAt)
                        .Skip((_currentPage - 1) * _pageSize)
                        .Take(_pageSize)
                        .AsNoTracking()
                        .ToList();

                    if (!campaigns.Any()) 
                        return new List<ManualCampaignInfoDto>();

                    var allProductCodes = campaigns.SelectMany(c => c.Products).Select(p => p.ProductCode).Distinct().ToList();

                    var productNamesList = db.CostCalculations
                        .Where(c => allProductCodes.Contains(c.ProductCode))
                        .Select(c => new { c.ProductCode, c.ProductName })
                        .AsNoTracking()
                        .ToList();

                    var productNames = productNamesList
                        .GroupBy(x => x.ProductCode)
                        .ToDictionary(g => g.Key, g => g.First().ProductName);

                    return campaigns.Select(c => new ManualCampaignInfoDto
                    {
                        Id = c.Id,
                        Description = c.Description,
                        Category = c.Category == "WhiteGoods" ? "Beyaz Eşya" : "KEA",
                        CreatedAt = c.CreatedAt,
                        Products = c.Products.Select(p => new ManualCampaignProductDto
                        {
                            ProductCode = p.ProductCode,
                            ProductName = productNames.TryGetValue(p.ProductCode, out var name) ? name : "Bilinmeyen Ürün"
                        }).ToList()
                    }).ToList();
                });
                
                if (result != null && result.Count > 0)
                {
                    _campaigns.AddRange(result);
                    IcCampaigns.ItemsSource = null; // Refresh binding
                    IcCampaigns.ItemsSource = _campaigns;
                    IcCampaigns.Visibility = Visibility.Visible;
                    
                    if (result.Count < _pageSize)
                        _isLastPage = true;
                    else
                        _currentPage++;
                }
                else
                {
                    _isLastPage = true;
                    if (isReset) EmptyState.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MainSnackbar.MessageQueue?.Enqueue($"Hata: {ex.Message}");
                if (isReset) EmptyState.Visibility = Visibility.Visible;
            }
            finally
            {
                _isLoading = false;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                PagingLoading.Visibility = Visibility.Collapsed;
            }
        }

        #region Search Navigation Logic
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformSearch();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnNextMatch_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async void PerformSearch()
        {
            ResetHighlights();
            _matchingBorders.Clear();
            _currentMatchIndex = -1;

            string query = TxtSearch.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                PnlSearchNav.Visibility = Visibility.Collapsed;
                return;
            }

            // 1. Expand campaigns containing any match
            bool anyExpanded = false;
            foreach (var campaign in _campaigns)
            {
                if (campaign.Products == null) continue;
                bool hasMatch = campaign.Products.Any(p =>
                    (p.ProductCode ?? "").ToLowerInvariant().Contains(query) ||
                    (p.ProductName ?? "").ToLowerInvariant().Contains(query)
                );
                if (hasMatch && !campaign.IsExpanded)
                {
                    campaign.IsExpanded = true;
                    anyExpanded = true;
                }
            }

            // 2. If any campaign was expanded, wait briefly for WPF layout to generate elements
            if (anyExpanded)
            {
                await Task.Delay(50);
            }

            // 3. Scan visual tree for matching borders
            foreach (var border in FindVisualChildren<Border>(IcCampaigns))
            {
                if (border.Tag?.ToString() == "ProductCard" && border.DataContext is ManualCampaignProductDto product)
                {
                    bool isMatch = (product.ProductCode ?? "").ToLowerInvariant().Contains(query) ||
                                  (product.ProductName ?? "").ToLowerInvariant().Contains(query);
                    if (isMatch)
                    {
                        _matchingBorders.Add(border);
                        border.Background = Brush("#FEF08A"); // standard highlight (soft yellow)
                    }
                }
            }

            PnlSearchNav.Visibility = Visibility.Visible;

            if (_matchingBorders.Count > 0)
            {
                _currentMatchIndex = 0;
                HighlightActiveMatch();
            }
            else
            {
                TxtMatchCount.Text = "0 / 0";
            }
        }

        private void HighlightActiveMatch()
        {
            if (_matchingBorders.Count == 0 || _currentMatchIndex < 0 || _currentMatchIndex >= _matchingBorders.Count) return;

            for (int i = 0; i < _matchingBorders.Count; i++)
            {
                var border = _matchingBorders[i];
                if (i == _currentMatchIndex)
                {
                    border.Background = Brush("#FDE047"); // active match (darker yellow)
                    border.BorderBrush = Brush("#0891B2");
                    border.BorderThickness = new Thickness(2.5);
                    border.BringIntoView();
                }
                else
                {
                    border.Background = Brush("#FEF08A");
                    border.BorderBrush = Brush("#E2E8F0");
                    border.BorderThickness = new Thickness(1);
                }
            }

            TxtMatchCount.Text = $"{_currentMatchIndex + 1} / {_matchingBorders.Count}";
        }

        private void BtnPrevMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_matchingBorders.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex - 1 + _matchingBorders.Count) % _matchingBorders.Count;
            HighlightActiveMatch();
        }

        private void BtnNextMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_matchingBorders.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex + 1) % _matchingBorders.Count;
            HighlightActiveMatch();
        }

        private void BtnToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ManualCampaignInfoDto campaign)
            {
                campaign.IsExpanded = !campaign.IsExpanded;
            }
        }

        private void ResetHighlights()
        {
            foreach (var border in FindVisualChildren<Border>(IcCampaigns))
            {
                if (border.Tag?.ToString() == "ProductCard" && border.DataContext is ManualCampaignProductDto)
                {
                    border.Background = Brush("#F8FAFC");
                    border.BorderBrush = Brush("#E2E8F0");
                    border.BorderThickness = new Thickness(1);
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }
                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private static SolidColorBrush Brush(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));
        #endregion
    }
}
