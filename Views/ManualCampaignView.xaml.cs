using Synctool.Data;
using Synctool.Models;
using Synctool.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Synctool.Views
{
    public partial class ManualCampaignView : UserControl
    {
        public ManualCampaignView()
        {
            InitializeComponent();
            TxtTriggerProductCodes.TextChanged += TxtProductCodes_TextChanged;
            TxtTargetProductCodes.TextChanged += TxtProductCodes_TextChanged;
        }

        private void TxtProductCodes_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private async void UpdatePreview()
        {
            var triggerCodes = GetParsedCodes(TxtTriggerProductCodes.Text);
            var targetCodes = GetParsedCodes(TxtTargetProductCodes.Text);
            var codes = triggerCodes.Concat(targetCodes).Distinct().ToList();

            if (!codes.Any())
            {
                LstPreview.ItemsSource = null;
                return;
            }

            // DB'de var mı kontrolü (opsiyonel görsel geri bildirim)
            var matchedCodes = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                var dbCodes = db.CostCalculations
                    .Where(c => codes.Contains(c.ProductCode))
                    .Select(c => c.ProductCode)
                    .Distinct()
                    .ToList();
                return dbCodes;
            });

            LstPreview.ItemsSource = codes.Select(c => new 
            { 
                Code = c, 
                Icon = matchedCodes.Contains(c) ? "CheckCircle" : "HelpCircleOutline",
                Color = matchedCodes.Contains(c) ? "#10B981" : "#94A3B8",
                Status = matchedCodes.Contains(c) ? "Sistemde Kayıtlı" : "Henüz Kayıtlı Değil"
            }).Take(100).ToList();
        }

        private List<string> GetParsedCodes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            return text
                .Split(new[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbCategory.SelectedItem == null)
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen bir kategori seçin.");
                return;
            }

            var triggerCodes = GetParsedCodes(TxtTriggerProductCodes.Text);
            var targetCodes = GetParsedCodes(TxtTargetProductCodes.Text);
            
            if (!triggerCodes.Any() && !targetCodes.Any())
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen en az bir ürün kodu girin.");
                return;
            }

            string description = TxtDescription.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                MainSnackbar.MessageQueue?.Enqueue("Lütfen kampanya açıklaması yazın.");
                return;
            }

            decimal? discountPrice = null;
            if (!string.IsNullOrWhiteSpace(TxtDiscountPrice.Text))
            {
                if (decimal.TryParse(TxtDiscountPrice.Text.Replace(",", "."), out decimal parsedDiscount))
                {
                    discountPrice = parsedDiscount;
                }
                else
                {
                    MainSnackbar.MessageQueue?.Enqueue("Lütfen geçerli bir indirim tutarı girin.");
                    return;
                }
            }

            var categoryItem = CmbCategory.SelectedItem as ComboBoxItem;
            string categoryTag = categoryItem?.Tag?.ToString() ?? "WhiteGoods";

            try
            {
                BtnSave.IsEnabled = false;

                int savedCount = 0;
                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    
                    var allCodes = triggerCodes.Concat(targetCodes).Distinct().ToList();

                    var validCodes = db.CostCalculations
                        .Where(c => c.SourceTable == categoryTag && allCodes.Contains(c.ProductCode.Trim()))
                        .Select(c => c.ProductCode.Trim())
                        .Distinct()
                        .ToList();

                    if (!validCodes.Any())
                    {
                        throw new InvalidOperationException("Girdiğiniz kodların hiçbiri seçili kategoride sistemde kayıtlı değil. Lütfen kodları kontrol edin.");
                    }

                    var campaign = new ManualCampaign
                    {
                        Description = description,
                        Category = categoryTag,
                        DiscountPrice = discountPrice,
                        CreatedAt = DateTime.Now
                    };
                    db.ManualCampaigns.Add(campaign);
                    db.SaveChanges();

                    var products = new List<ManualCampaignProduct>();

                    foreach (var code in triggerCodes)
                    {
                        if (validCodes.Contains(code))
                        {
                            products.Add(new ManualCampaignProduct
                            {
                                ManualCampaignId = campaign.Id,
                                ProductCode = code,
                                IsTargetProduct = false
                            });
                        }
                    }

                    foreach (var code in targetCodes)
                    {
                        if (validCodes.Contains(code))
                        {
                            products.Add(new ManualCampaignProduct
                            {
                                ManualCampaignId = campaign.Id,
                                ProductCode = code,
                                IsTargetProduct = true
                            });
                        }
                    }

                    db.ManualCampaignProducts.AddRange(products);
                    db.SaveChanges();
                    savedCount = products.Count;
                });

                MainSnackbar.MessageQueue?.Enqueue($"✅ {savedCount} geçerli ürün için kampanya tanımlandı.");
                
                TxtTriggerProductCodes.Text = "";
                TxtTargetProductCodes.Text = "";
                TxtDiscountPrice.Text = "";
                TxtDescription.Text = "";
                UpdatePreview();
            }
            catch (InvalidOperationException ex)
            {
                await ModernDialogService.ShowAsync("Uyarı", ex.Message, ModernDialogType.Warning);
            }
            catch (Exception ex)
            {
                await ModernDialogService.ShowAsync("Hata", $"Kampanya kaydedilirken hata oluştu:\n{ex.Message}", ModernDialogType.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
            }
        }
    }
}
