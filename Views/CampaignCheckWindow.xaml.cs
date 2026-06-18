using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Synctool.Models;
using Synctool.Services;
using Microsoft.EntityFrameworkCore;
using Synctool.Services;

namespace Synctool.Views
{
    public class CartCampaignViewModel
    {
        public int CampaignId { get; set; }
        public string CampaignCode { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public bool HasTargetProducts { get; set; } = true;
    }

    public class LinkedProductViewModel
    {
        public string Column1 { get; set; } = string.Empty;
        public string Column2 { get; set; } = string.Empty;
        public string Column3 { get; set; } = string.Empty;
    }

    public class CartItemCampaignViewModel
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public List<CartCampaignViewModel> Campaigns { get; set; } = new();

        public string CampaignCountText => $"{Campaigns.Count} Kampanya";
        public string CampaignCountColor => Campaigns.Count > 0 ? "#10B981" : "#94A3B8";
    }

    public partial class CampaignCheckWindow : Window
    {
        private readonly List<CartItem> _cartItems;
        private Dictionary<string, List<CartCampaignViewModel>> _campaignData = new();
        private List<CartItemCampaignViewModel> _viewModels = new();

        public CampaignCheckWindow(List<CartItem> cartItems)
        {
            InitializeComponent();
            _cartItems = cartItems;
            Loaded += CampaignCheckWindow_Loaded;
        }

        private async void CampaignCheckWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var productCodes = _cartItems.Select(c => c.ProductCode ?? string.Empty).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

            LoadingOverlay.Visibility = Visibility.Visible;

            await Task.Run(() => 
            {
                using var db = new Synctool.Data.AppDbContext();
                
                var matchedCampaigns = db.ManualCampaignProducts
                    .Include(mcp => mcp.ManualCampaign)
                    .Where(mcp => productCodes.Contains(mcp.ProductCode) && !mcp.IsTargetProduct)
                    .AsNoTracking()
                    .ToList();

                foreach (var code in productCodes)
                {
                    var campaignsForCode = matchedCampaigns
                        .Where(mcp => mcp.ProductCode == code)
                        .Select(mcp => new CartCampaignViewModel
                        {
                            CampaignId = mcp.ManualCampaign.Id,
                            CampaignCode = $"KMP-{mcp.ManualCampaign.Id}",
                            ShortDescription = mcp.ManualCampaign.Description,
                            DiscountAmount = mcp.ManualCampaign.DiscountPrice ?? 0m,
                            HasTargetProducts = true
                        }).ToList();

                    _campaignData[code] = campaignsForCode;
                }
            });

            _viewModels = _cartItems.Select(c => new CartItemCampaignViewModel
            {
                ProductCode = c.ProductCode ?? string.Empty,
                ProductName = c.ProductName ?? string.Empty,
                Campaigns = _campaignData.ContainsKey(c.ProductCode ?? string.Empty) ? _campaignData[c.ProductCode ?? string.Empty] : new List<CartCampaignViewModel>()
            }).ToList();

            ListCartItems.ItemsSource = _viewModels;

            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (_viewModels.Any())
            {
                ListCartItems.SelectedIndex = 0;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ListCartItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListCartItems.SelectedItem is CartItemCampaignViewModel selected)
            {
                PanelNoSelection.Visibility = Visibility.Collapsed;
                PanelCampaigns.Visibility = Visibility.Visible;
                PanelLinkedProducts.Visibility = Visibility.Collapsed; // Reset details view

                TxtSelectedProductName.Text = selected.ProductName;
                ItemsCampaigns.ItemsSource = selected.Campaigns;
            }
            else
            {
                PanelNoSelection.Visibility = Visibility.Visible;
                PanelCampaigns.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnViewProducts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int campaignId)
            {
                PanelCampaigns.Visibility = Visibility.Collapsed;
                PanelLinkedProducts.Visibility = Visibility.Visible;
                TxtLinkedSheetName.Text = $"Kampanya Kodu: KMP-{campaignId}";

                LinkedProductsLoadingOverlay.Visibility = Visibility.Visible;

                var products = await Task.Run(() => {
                    using var db = new Synctool.Data.AppDbContext();
                    
                    var targetProducts = db.ManualCampaignProducts
                        .Where(mcp => mcp.ManualCampaignId == campaignId && mcp.IsTargetProduct)
                        .Select(mcp => mcp.ProductCode)
                        .ToList();

                    var dbNames = db.CostCalculations
                        .Where(c => targetProducts.Contains(c.ProductCode))
                        .Select(c => new { c.ProductCode, c.ProductName, c.SourceTable })
                        .AsNoTracking()
                        .ToList()
                        .GroupBy(x => x.ProductCode)
                        .ToDictionary(g => g.Key, g => g.First());

                    return targetProducts.Select(code => new LinkedProductViewModel
                    {
                        Column1 = code,
                        Column2 = dbNames.TryGetValue(code, out var info) ? info.ProductName : "Bilinmeyen Ürün",
                        Column3 = dbNames.TryGetValue(code, out var i) ? (i.SourceTable == "WhiteGoods" ? "Beyaz Eşya" : "KEA") : ""
                    }).ToList();
                });
                
                GridLinkedProducts.ItemsSource = products;

                LinkedProductsLoadingOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Bu kampanya için geçerli bir ürün listesi bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnBackToCampaigns_Click(object sender, RoutedEventArgs e)
        {
            PanelLinkedProducts.Visibility = Visibility.Collapsed;
            PanelCampaigns.Visibility = Visibility.Visible;
        }
    }
}
