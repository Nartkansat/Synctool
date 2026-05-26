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
            TxtProductCodes.TextChanged += TxtProductCodes_TextChanged;
        }

        private void TxtProductCodes_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private async void UpdatePreview()
        {
            var codes = GetParsedCodes();
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

        private List<string> GetParsedCodes()
        {
            if (string.IsNullOrWhiteSpace(TxtProductCodes.Text)) return new List<string>();

            return TxtProductCodes.Text
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

            var codes = GetParsedCodes();
            if (!codes.Any())
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

            var categoryItem = CmbCategory.SelectedItem as ComboBoxItem;
            string categoryTag = categoryItem?.Tag?.ToString() ?? "WhiteGoods";

            try
            {
                BtnSave.IsEnabled = false;

                int savedCount = 0;
                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    
                    // 1. Sadece sistemde (CostCalculations) kayıtlı olan kodları filtrele
                    var validCodes = db.CostCalculations
                        .Where(c => c.SourceTable == categoryTag && codes.Contains(c.ProductCode.Trim()))
                        .Select(c => c.ProductCode.Trim())
                        .Distinct()
                        .ToList();

                    if (!validCodes.Any())
                    {
                        throw new InvalidOperationException("Girdiğiniz kodların hiçbiri seçili kategoride sistemde kayıtlı değil. Lütfen kodları kontrol edin.");
                    }

                    // 2. Yeni Kampanya Başlığı
                    var campaign = new ManualCampaign
                    {
                        Description = description,
                        Category = categoryTag,
                        CreatedAt = DateTime.Now
                    };
                    db.ManualCampaigns.Add(campaign);
                    db.SaveChanges();

                    // 4. Sadece geçerli ürünleri bağla
                    var products = validCodes.Select(code => new ManualCampaignProduct
                    {
                        ManualCampaignId = campaign.Id,
                        ProductCode = code
                    }).ToList();

                    db.ManualCampaignProducts.AddRange(products);
                    db.SaveChanges();
                    savedCount = validCodes.Count;
                });

                MainSnackbar.MessageQueue?.Enqueue($"✅ {savedCount} geçerli ürün için kampanya tanımlandı.");
                
                // Temizle
                TxtProductCodes.Text = "";
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
