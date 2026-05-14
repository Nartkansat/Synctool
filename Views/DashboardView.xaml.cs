using ArcelikApp.Data;
using ArcelikApp.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArcelikExcelApp.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadDashboardDataAsync();
        }

        private async Task LoadDashboardDataAsync()
        {
            if (AuthService.CurrentUser == null) return;
            int currentUserId = AuthService.CurrentUser.Id;

            try
            {
                var data = await Task.Run(() =>
                {
                    using var db = new AppDbContext();

                    // 1. Temel Sayısal Veriler
                    int keaCount = db.KeaProducts.Count();
                    int wgCount = db.WhiteGoodsProducts.Count();
                    int totalProducts = keaCount + wgCount;
                    
                    int totalCampaigns = db.OlizCampaigns.Count();
                    int totalCalculations = db.CostCalculations.Count();
                    int totalFiles = db.UploadedFiles.Count();

                    // 2. Ortalama Fiyat Analizleri
                    decimal keaAvg = 0;
                    if (keaCount > 0)
                    {
                        keaAvg = db.KeaProducts.Where(x => x.WholesalePrice60 > 0).Average(x => (decimal?)x.WholesalePrice60) ?? 0;
                    }
                    
                    decimal wgAvg = 0;
                    if (wgCount > 0)
                    {
                        wgAvg = db.WhiteGoodsProducts.Where(x => x.WholesalePrice60 > 0).Average(x => (decimal?)x.WholesalePrice60) ?? 0;
                    }

                    // 3. Kampanya Kullanım Oranı
                    int campaignAppliedCount = 0;
                    double campaignRatio = 0;
                    if (totalCalculations > 0)
                    {
                        campaignAppliedCount = db.CostCalculations.Count(x => x.PriceConversion > 0);
                        campaignRatio = ((double)campaignAppliedCount / totalCalculations) * 100;
                    }

                    // 4. Kategori Dağılımı
                    double keaRatio = totalProducts > 0 ? ((double)keaCount / totalProducts) * 100 : 0;
                    double wgRatio = totalProducts > 0 ? ((double)wgCount / totalProducts) * 100 : 0;

                    // 5. Alt Kategori Dağılımı (KEA ve WG Birleşik)
                    var keaSubCats = db.KeaProducts
                        .GroupBy(x => x.ExcelFileType)
                        .Select(g => new { Name = g.Key ?? "Diğer", Count = g.Count() })
                        .ToList();

                    var wgSubCats = db.WhiteGoodsProducts
                        .GroupBy(x => x.ExcelFileType)
                        .Select(g => new { Name = g.Key ?? "Diğer", Count = g.Count() })
                        .ToList();

                    var colors = new[] { "#6366F1", "#14B8A6", "#F59E0B", "#8B5CF6", "#EC4899", "#0EA5E9" };

                    var allSubCats = keaSubCats.Concat(wgSubCats)
                        .GroupBy(x => x.Name)
                        .Select(g => new 
                        { 
                            CategoryName = g.Key, 
                            Count = g.Sum(x => x.Count),
                            Ratio = totalProducts > 0 ? ((double)g.Sum(x => x.Count) / totalProducts) * 100 : 0
                        })
                        .OrderByDescending(x => x.Count)
                        .Take(6)
                        .Select((x, index) => new 
                        {
                            x.CategoryName,
                            x.Ratio,
                            RatioText = $"%{x.Ratio:F1}",
                            Color = colors[index % colors.Length]
                        })
                        .ToList();

                    // 6. Son Bildirimler (FİLTRELİ)
                    var recentNotifications = db.Notifications
                                                .Where(n => n.UserId == currentUserId || n.UserId == null)
                                                .OrderByDescending(x => x.CreatedAt)
                                                .Take(5)
                                                .ToList();
                                                
                    // 7. Sistem Güvenlik ve Durum Bilgisi
                    var lastUploadedFile = db.UploadedFiles.OrderByDescending(x => x.Id).FirstOrDefault();
                    string lastUpdateStr = lastUploadedFile != null ? lastUploadedFile.UploadDate : DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                    // 8. Kullanıcı İstatistikleri
                    int totalUsers = db.Users.Count();

                    return new
                    {
                        keaCount, wgCount, totalProducts, totalCampaigns, totalCalculations, totalFiles,
                        keaAvg, wgAvg, campaignRatio, keaRatio, wgRatio, allSubCats,
                        recentNotifications, totalUsers, lastUpdateStr
                    };
                });

                // UI güncellemelerini ana thread'de yap
                TxtTotalProducts.Text = data.totalProducts.ToString("N0");
                TxtTotalFiles.Text = data.totalFiles.ToString("N0");
                TxtTotalCampaigns.Text = data.totalCampaigns.ToString("N0");
                TxtTotalUsers.Text = data.totalUsers.ToString();

                TxtKeaAvgPrice.Text = $"{data.keaAvg:N0} ₺";
                TxtWgAvgPrice.Text = $"{data.wgAvg:N0} ₺";

                TxtCampaignRatio.Text = $"%{data.campaignRatio:F1}";
                ProgCampaign.Value = data.campaignRatio;

                TxtKeaRatio.Text = $"%{data.keaRatio:F1}";
                ProgKea.Value = data.keaRatio;

                TxtWgRatio.Text = $"%{data.wgRatio:F1}";
                ProgWg.Value = data.wgRatio;

                ListSubCategories.ItemsSource = data.allSubCats;

                // Kullanıcı Bilgileri (AuthService üzerinden)
                var user = AuthService.CurrentUser;
                if (user != null)
                {
                    TxtUserDealer.Text = string.IsNullOrEmpty(user.DealerName) ? "Belirtilmemiş" : user.DealerName;
                    TxtUserEmail.Text = user.Email;
                    TxtUserRole.Text = user.Role;

                    // Lisans Kalan Gün Hesaplama
                    if (user.LicenseExpirationDate.HasValue)
                    {
                        var remainingDays = (user.LicenseExpirationDate.Value - DateTime.Now).Days;
                        if (remainingDays > 0)
                            TxtLicenseDays.Text = $" ({remainingDays} gün kaldı)";
                        else if (remainingDays == 0)
                            TxtLicenseDays.Text = " (Bugün son gün!)";
                        else
                            TxtLicenseDays.Text = " (Süresi doldu)";
                    }
                    else
                    {
                        TxtLicenseDays.Text = " (Sınırsız)";
                    }
                }

                ListSubCategories.ItemsSource = data.allSubCats;

                TxtSystemStatus.Text = "Sistem Güvende • Aktif";
                TxtLastUpdate.Text = data.lastUpdateStr;

                ListRecentNotifications.ItemsSource = data.recentNotifications;
                
                // Show Empty State if No Notifications
                EmptyNotificationsPanel.Visibility = data.recentNotifications.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtSystemStatus.Text = "Bağlantı Hatası!";
                TxtSystemStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                await ModernDialogService.ShowAsync("Veri Hatası", $"Dashboard verileri yüklenirken hata oluştu: {ex.Message}", ModernDialogType.Warning);
            }
        }
    }
}

