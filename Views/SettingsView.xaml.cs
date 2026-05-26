using Synctool.Data;
using Synctool.Services;
using Synctool.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf;

namespace Synctool.Views
{
    public partial class SettingsView : UserControl
    {
        private List<string> _tablesPendingReset = new List<string>();

        public SettingsView()
        {
            InitializeComponent();
            this.Loaded += (s, e) => ValidateSetPassword("");
            if (AuthService.CurrentUser != null)
            {
                TxtProfileName.Text = AuthService.CurrentUser.Username;
                TxtProfileRole.Text = AuthService.CurrentUser.Role + " Yetkisi";

                // Kullanım kılavuzu tüm giriş yapmış kullanıcılara açık
                TabUserGuide.Visibility = Visibility.Visible;
                LoadUserGuide();

                // Yetki Kontrolü: Sadece Admin veri sıfırlama panelini görebilir ve düzenleyebilir
                if (AuthService.CurrentUser.Role == "Admin")
                {
                    TabDataManagement.Visibility = Visibility.Visible;
                    TabAgreementManagement.Visibility = Visibility.Visible;
                    BtnEditUserGuideToggle.Visibility = Visibility.Visible;

                    // Mevcut verileri yükle
                    LoadCurrentAgreement();
                    LoadAgreementsHistoryAndConsents();
                }
                else
                {
                    TabDataManagement.Visibility = Visibility.Collapsed;
                    TabAgreementManagement.Visibility = Visibility.Collapsed;
                    BtnEditUserGuideToggle.Visibility = Visibility.Collapsed;
                    BtnSaveUserGuide.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Kullanıcı oturumu yoksa (güvenlik için) paneli gizle
                TabDataManagement.Visibility = Visibility.Collapsed;
                TabAgreementManagement.Visibility = Visibility.Collapsed;
                TabUserGuide.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadCurrentAgreement()
        {
            var latest = await Task.Run(() => AuthService.GetLatestAgreement());
            if (latest != null)
            {
                TxtAgreementVersion.Text = latest.Version;
                TxtAgreementContent.Text = latest.Content;
            }
        }

        private void BtnNewAgreementInit_Click(object sender, RoutedEventArgs e)
        {
            PanelNormalAgreementView.Visibility = Visibility.Collapsed;
            PanelPublishAgreement.Visibility = Visibility.Visible;
            TxtAgreementVersion.Text = "";
            TxtAgreementContent.Text = "";
        }

        private void BtnCancelPublish_Click(object sender, RoutedEventArgs e)
        {
            PanelNormalAgreementView.Visibility = Visibility.Visible;
            PanelPublishAgreement.Visibility = Visibility.Collapsed;
        }

        private async void BtnSaveAgreement_Click(object sender, RoutedEventArgs e)
        {
            string version = TxtAgreementVersion.Text.Trim();
            string content = TxtAgreementContent.Text.Trim();

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(content))
            {
                ShowToast("Versiyon ve içerik boş bırakılamaz.");
                return;
            }

            BtnSaveAgreement.IsEnabled = false;
            bool success = await Task.Run(() => AuthService.SaveNewAgreement(version, content));
            BtnSaveAgreement.IsEnabled = true;

            if (success)
            {
                ShowToast("Yeni sözleşme başarıyla yayınlandı.");
                PanelNormalAgreementView.Visibility = Visibility.Visible;
                PanelPublishAgreement.Visibility = Visibility.Collapsed;
                LoadAgreementsHistoryAndConsents();
            }
            else
            {
                ShowToast("Sözleşme kaydedilirken bir hata oluştu.");
            }
        }

        private async void BtnUpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            string current = TxtCurrentPassword.Password;
            string newPass = TxtNewPassword.Password;
            string confirm = TxtConfirmPassword.Password;

            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(newPass) || string.IsNullOrEmpty(confirm))
            {
                ShowError("Lütfen tüm alanları doldurun.");
                return;
            }

            if (newPass != confirm)
            {
                ShowError("Yeni şifreler uyuşmuyor.");
                return;
            }

            if (newPass.Length < 8 || !newPass.Any(char.IsUpper) || !newPass.Any(char.IsLower) || !newPass.Any(c => !char.IsLetterOrDigit(c)))
            {
                ShowError("Yeni şifre en az 8 karakter olmalı, en az 1 büyük harf, 1 küçük harf ve 1 özel karakter (sembol) içermelidir.");
                return;
            }

            if (AuthService.CurrentUser == null) return;

            if (!SecurityHelper.VerifyPassword(current, AuthService.CurrentUser.PasswordHash))
            {
                ShowError("Mevcut şifreniz hatalı.");
                return;
            }

            try
            {
                int userId = AuthService.CurrentUser.Id;
                string newHash = SecurityHelper.HashPassword(newPass);
                
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var user = db.Users.Find(userId);
                    if (user != null)
                    {
                        user.PasswordHash = newHash;
                        db.SaveChanges();
                    }
                });
                
                // Update current user hash as well
                AuthService.CurrentUser.PasswordHash = newHash;
                
                ShowToast("Şifreniz başarıyla güncellendi.");
                TxtCurrentPassword.Password = "";
                TxtNewPassword.Password = "";
                TxtConfirmPassword.Password = "";
                ValidateSetPassword("");
                TxtSettingsError.Visibility = Visibility.Collapsed;
            }
            catch (System.Exception ex)
            {
                ShowError($"Hata: {ex.Message}");
            }
        }

        private async void ShowToast(string message)
        {
            TxtToastMessage.Text = message;
            ToastCard.Visibility = Visibility.Visible;

            // Fade and Slide In
            var fadeIn = new DoubleAnimation(1, System.TimeSpan.FromMilliseconds(400));
            var slideIn = new DoubleAnimation(0, System.TimeSpan.FromMilliseconds(400));
            
            ToastCard.BeginAnimation(OpacityProperty, fadeIn);
            ((TranslateTransform)ToastCard.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);

            await Task.Delay(4000);
            
            if (ToastCard.Visibility == Visibility.Visible)
                HideToast();
        }

        private void BtnCloseToast_Click(object sender, RoutedEventArgs e)
        {
            HideToast();
        }

        private void HideToast()
        {
            // Fade and Slide Out
            var fadeOut = new DoubleAnimation(0, System.TimeSpan.FromMilliseconds(400));
            var slideOut = new DoubleAnimation(50, System.TimeSpan.FromMilliseconds(400));

            fadeOut.Completed += (s, e) => ToastCard.Visibility = Visibility.Collapsed;
            
            ToastCard.BeginAnimation(OpacityProperty, fadeOut);
            ((TranslateTransform)ToastCard.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        private void ShowError(string message)
        {
            TxtSettingsError.Text = message;
            TxtSettingsError.Visibility = Visibility.Visible;
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            SetAllCheckboxes(true);
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            SetAllCheckboxes(false);
        }

        private void SetAllCheckboxes(bool isChecked)
        {
            ChkCostCalculations.IsChecked = isChecked;
            ChkOlizCampaigns.IsChecked = isChecked;
            ChkKeaProducts.IsChecked = isChecked;
            ChkWhiteGoodsProducts.IsChecked = isChecked;
            ChkHistoricalKea.IsChecked = isChecked;
            ChkHistoricalWhiteGoods.IsChecked = isChecked;
            ChkManualCampaigns.IsChecked = isChecked;
            ChkManualCampaignProducts.IsChecked = isChecked;
            ChkUploadedFiles.IsChecked = isChecked;
        }

        private async void BtnResetTables_Click(object sender, RoutedEventArgs e)
        {
            await Task.CompletedTask;
            // Güvenlik Kontrolü: Kod seviyesinde yetki doğrulaması
            if (AuthService.CurrentUser?.Role != "Admin")
            {
                ShowModernAlert("Yetki Hatası", "Bu işlem için yönetici yetkisi gereklidir. Lütfen sistem yöneticisiyle iletişime geçin.", false);
                return;
            }

            var tablesToReset = new List<string>();
            if (ChkCostCalculations.IsChecked == true) tablesToReset.Add("CostCalculations");
            if (ChkOlizCampaigns.IsChecked == true) tablesToReset.Add("OlizCampaigns");
            if (ChkKeaProducts.IsChecked == true) tablesToReset.Add("KeaProducts");
            if (ChkWhiteGoodsProducts.IsChecked == true) tablesToReset.Add("WhiteGoodsProducts");
            
            if (ChkHistoricalKea.IsChecked == true) tablesToReset.Add("HistoricalKeaProducts");
            if (ChkHistoricalWhiteGoods.IsChecked == true) tablesToReset.Add("HistoricalWhiteGoodsProducts");
            if (ChkManualCampaigns.IsChecked == true) tablesToReset.Add("ManualCampaigns");
            if (ChkManualCampaignProducts.IsChecked == true) tablesToReset.Add("ManualCampaignProducts");
            if (ChkUploadedFiles.IsChecked == true) tablesToReset.Add("UploadedFiles");

            if (tablesToReset.Count == 0)
            {
                ShowModernAlert("Uyarı", "Lütfen sıfırlanacak en az bir tablo seçin.", false);
                return;
            }

            _tablesPendingReset = tablesToReset;
            ShowModernAlert("Tablo Sıfırlama Onayı", $"{string.Join(", ", tablesToReset)} tabloları tamamen boşaltılacak ve ID'ler sıfırlanacak. Bu işlem geri alınamaz.\n\nDevam etmek istiyor musunuz?", true);
        }

        private void ShowModernAlert(string title, string message, bool isConfirmation)
        {
            TxtAlertTitle.Text = title;
            TxtAlertMessage.Text = message;

            if (isConfirmation)
            {
                BtnCancelAlert.Visibility = Visibility.Visible;
                BtnConfirmAction.Content = "Evet, Sıfırla";
                BtnConfirmAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E02020"));
                BtnConfirmAction.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E02020"));
                
                IconAlert.Kind = PackIconKind.AlertCircleOutline;
                BorderAlertIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                IconAlert.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
            }
            else
            {
                BtnCancelAlert.Visibility = Visibility.Collapsed;
                BtnConfirmAction.Content = "Anladım";
                BtnConfirmAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A24"));
                BtnConfirmAction.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A24"));

                IconAlert.Kind = PackIconKind.InformationOutline;
                BorderAlertIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                IconAlert.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
            }

            AlertOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCancelAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertOverlay.Visibility = Visibility.Collapsed;
            _tablesPendingReset.Clear();
        }

        private void BtnConfirmAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertOverlay.Visibility = Visibility.Collapsed;
            
            // Sıfırlama işlemi için şifre teyit overlay'ini aç
            TxtConfirmResetPassword.Password = "";
            TxtConfirmResetPasswordError.Visibility = Visibility.Collapsed;
            PasswordConfirmOverlay.Visibility = Visibility.Visible;
            TxtConfirmResetPassword.Focus();
        }

        private void BtnCancelPasswordConfirm_Click(object sender, RoutedEventArgs e)
        {
            PasswordConfirmOverlay.Visibility = Visibility.Collapsed;
            _tablesPendingReset.Clear();
        }

        private async void BtnVerifyPasswordAndReset_Click(object sender, RoutedEventArgs e)
        {
            if (AuthService.CurrentUser == null) return;

            string password = TxtConfirmResetPassword.Password;
            if (string.IsNullOrEmpty(password))
            {
                TxtConfirmResetPasswordError.Text = "Lütfen yönetici şifrenizi girin.";
                TxtConfirmResetPasswordError.Visibility = Visibility.Visible;
                return;
            }

            if (!SecurityHelper.VerifyPassword(password, AuthService.CurrentUser.PasswordHash))
            {
                TxtConfirmResetPasswordError.Text = "Şifre hatalı. Sıfırlama işlemi reddedildi.";
                TxtConfirmResetPasswordError.Visibility = Visibility.Visible;
                return;
            }

            PasswordConfirmOverlay.Visibility = Visibility.Collapsed;

            try
            {
                using (var db = new AppDbContext())
                {
                    // MySQL'de foreign key kısıtlamalarına takılmamak için geçici olarak devre dışı bırakıyoruz
                    await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                    foreach (var tableName in _tablesPendingReset)
                    {
                        // Güvenlik: Sadece izin verilen tablo isimlerini çalıştır
                        string? sql = tableName switch
                        {
                            "CostCalculations"     => "TRUNCATE TABLE CostCalculations",
                            "OlizCampaigns"       => "TRUNCATE TABLE OlizCampaigns",
                            "KeaProducts"         => "TRUNCATE TABLE KeaProducts",
                            "WhiteGoodsProducts"  => "TRUNCATE TABLE WhiteGoodsProducts",
                            "HistoricalKeaProducts" => "TRUNCATE TABLE HistoricalKeaProducts",
                            "HistoricalWhiteGoodsProducts" => "TRUNCATE TABLE HistoricalWhiteGoodsProducts",
                            "ManualCampaigns" => "TRUNCATE TABLE ManualCampaigns",
                            "ManualCampaignProducts" => "TRUNCATE TABLE ManualCampaignProducts",
                            "UploadedFiles" => "TRUNCATE TABLE UploadedFiles",
                            _ => null
                        };

                        if (sql != null)
                        {
                            await db.Database.ExecuteSqlRawAsync(sql);
                        }
                    }

                    // İşlem bitince tekrar aktif ediyoruz
                    await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                }
                ShowToast("Seçili tablolar başarıyla sıfırlandı.");
                SetAllCheckboxes(false);
                ChkSelectAll.IsChecked = false;
            }
            catch (System.Exception ex)
            {
                ShowModernAlert("Hata Oluştu", $"Sıfırlama sırasında teknik bir hata oluştu: {ex.Message}", false);
            }
            finally
            {
                _tablesPendingReset.Clear();
            }
        }

        private void TxtConfirmResetPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnVerifyPasswordAndReset_Click(sender, e);
            }
        }

        // ── Kullanım Kılavuzu İşlemleri ─────────────────────────────────────────

        private async void LoadUserGuide()
        {
            try
            {
                var content = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var guide = db.UserGuides.OrderByDescending(g => g.Id).FirstOrDefault();
                    return guide?.Content ?? string.Empty;
                });
                TxtUserGuideContent.Text = content;
                WebUserGuideView.NavigateToString(FormatUserGuideToHtml(content));
            }
            catch (System.Exception ex)
            {
                ShowError($"Kılavuz yüklenirken hata: {ex.Message}");
            }
        }

        private async void BtnSaveUserGuide_Click(object sender, RoutedEventArgs e)
        {
            string content = TxtUserGuideContent.Text.Trim();
            BtnSaveUserGuide.IsEnabled = false;

            try
            {
                bool success = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var guide = new UserGuide
                    {
                        Content = content,
                        UpdatedAt = System.DateTime.Now
                    };
                    db.UserGuides.Add(guide);
                    db.SaveChanges();
                    return true;
                });

                if (success)
                {
                    ShowToast("Kullanım kılavuzu başarıyla kaydedildi.");
                    
                    // Görünüm moduna geri dön
                    BorderUserGuideView.Visibility = Visibility.Visible;
                    BorderUserGuideEdit.Visibility = Visibility.Collapsed;
                    BtnSaveUserGuide.Visibility = Visibility.Collapsed;
                    BtnEditUserGuideToggle.Content = "Düzenle";
                    BtnEditUserGuideToggle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                    BtnEditUserGuideToggle.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                    
                    LoadUserGuide();
                }
                else
                {
                    ShowError("Kılavuz kaydedilirken hata oluştu.");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Hata: {ex.Message}");
            }
            finally
            {
                BtnSaveUserGuide.IsEnabled = true;
            }
        }

        private void BtnEditUserGuideToggle_Click(object sender, RoutedEventArgs e)
        {
            if (BorderUserGuideView.Visibility == Visibility.Visible)
            {
                // Düzenleme moduna geç
                BorderUserGuideView.Visibility = Visibility.Collapsed;
                BorderUserGuideEdit.Visibility = Visibility.Visible;
                BtnSaveUserGuide.Visibility = Visibility.Visible;
                BtnEditUserGuideToggle.Content = "Vazgeç";
                BtnEditUserGuideToggle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                BtnEditUserGuideToggle.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }
            else
            {
                // Görünüm moduna geri dön
                BorderUserGuideView.Visibility = Visibility.Visible;
                BorderUserGuideEdit.Visibility = Visibility.Collapsed;
                BtnSaveUserGuide.Visibility = Visibility.Collapsed;
                BtnEditUserGuideToggle.Content = "Düzenle";
                BtnEditUserGuideToggle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                BtnEditUserGuideToggle.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                
                // Kaydedilmemiş değişiklikleri geri al
                LoadUserGuide();
            }
        }

        private string FormatUserGuideToHtml(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return @"
<html>
<head>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <style>
        body {
            font-family: 'Segoe UI', -apple-system, sans-serif;
            color: #64748B;
            background-color: #FFFFFF;
            margin: 0;
            padding: 24px;
            text-align: center;
        }
    </style>
</head>
<body>
    <p>Kullanım kılavuzu henüz eklenmemiş.</p>
</body>
</html>";
            }

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            var htmlBuilder = new System.Text.StringBuilder();

            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("    <meta http-equiv='X-UA-Compatible' content='IE=edge'>");
            htmlBuilder.AppendLine("    <meta charset='utf-8'>");
            htmlBuilder.AppendLine("    <style>");
            htmlBuilder.AppendLine("        body {");
            htmlBuilder.AppendLine("            font-family: 'Segoe UI', -apple-system, sans-serif;");
            htmlBuilder.AppendLine("            color: #334155;");
            htmlBuilder.AppendLine("            background-color: #FFFFFF;");
            htmlBuilder.AppendLine("            line-height: 1.6;");
            htmlBuilder.AppendLine("            margin: 0;");
            htmlBuilder.AppendLine("            padding: 24px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        h1 {");
            htmlBuilder.AppendLine("            color: #1E293B;");
            htmlBuilder.AppendLine("            font-size: 20px;");
            htmlBuilder.AppendLine("            font-weight: 800;");
            htmlBuilder.AppendLine("            margin-top: 0;");
            htmlBuilder.AppendLine("            margin-bottom: 20px;");
            htmlBuilder.AppendLine("            padding-bottom: 12px;");
            htmlBuilder.AppendLine("            border-bottom: 2px solid #D97706;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        h2 {");
            htmlBuilder.AppendLine("            color: #1E293B;");
            htmlBuilder.AppendLine("            font-size: 15px;");
            htmlBuilder.AppendLine("            font-weight: 700;");
            htmlBuilder.AppendLine("            margin-top: 24px;");
            htmlBuilder.AppendLine("            margin-bottom: 12px;");
            htmlBuilder.AppendLine("            border-bottom: 1px dashed #CBD5E1;");
            htmlBuilder.AppendLine("            padding-bottom: 6px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        p {");
            htmlBuilder.AppendLine("            margin-top: 0;");
            htmlBuilder.AppendLine("            margin-bottom: 12px;");
            htmlBuilder.AppendLine("            font-size: 13px;");
            htmlBuilder.AppendLine("            color: #475569;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        ul {");
            htmlBuilder.AppendLine("            margin-top: 0;");
            htmlBuilder.AppendLine("            margin-bottom: 16px;");
            htmlBuilder.AppendLine("            padding-left: 20px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        li {");
            htmlBuilder.AppendLine("            font-size: 13px;");
            htmlBuilder.AppendLine("            color: #475569;");
            htmlBuilder.AppendLine("            margin-bottom: 6px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        .card {");
            htmlBuilder.AppendLine("            padding: 12px 16px;");
            htmlBuilder.AppendLine("            border-radius: 8px;");
            htmlBuilder.AppendLine("            margin-bottom: 16px;");
            htmlBuilder.AppendLine("            font-size: 13px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        .card-warning {");
            htmlBuilder.AppendLine("            background-color: #FEF2F2;");
            htmlBuilder.AppendLine("            border-left: 4px solid #EF4444;");
            htmlBuilder.AppendLine("            border-top: 1px solid #FEE2E2;");
            htmlBuilder.AppendLine("            border-right: 1px solid #FEE2E2;");
            htmlBuilder.AppendLine("            border-bottom: 1px solid #FEE2E2;");
            htmlBuilder.AppendLine("            color: #991B1B;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        .card-warning-title {");
            htmlBuilder.AppendLine("            font-weight: bold;");
            htmlBuilder.AppendLine("            color: #7F1D1D;");
            htmlBuilder.AppendLine("            margin-bottom: 4px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        .card-info {");
            htmlBuilder.AppendLine("            background-color: #F0F9FF;");
            htmlBuilder.AppendLine("            border-left: 4px solid #0284C7;");
            htmlBuilder.AppendLine("            border-top: 1px solid #E0F2FE;");
            htmlBuilder.AppendLine("            border-right: 1px solid #E0F2FE;");
            htmlBuilder.AppendLine("            border-bottom: 1px solid #E0F2FE;");
            htmlBuilder.AppendLine("            color: #0369A1;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("        .card-info-title {");
            htmlBuilder.AppendLine("            font-weight: bold;");
            htmlBuilder.AppendLine("            color: #0C4A6E;");
            htmlBuilder.AppendLine("            margin-bottom: 4px;");
            htmlBuilder.AppendLine("        }");
            htmlBuilder.AppendLine("    </style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            bool inList = false;
            bool isFirstLine = true;

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmedLine = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (inList)
                    {
                        htmlBuilder.AppendLine("    </ul>");
                        inList = false;
                    }
                    continue;
                }

                // ilk satır ana başlıktır (H1)
                if (isFirstLine)
                {
                    isFirstLine = false;
                    htmlBuilder.AppendLine($"    <h1>{System.Net.WebUtility.HtmlEncode(trimmedLine)}</h1>");
                    continue;
                }

                // * veya - ile başlayan satırlar listedir
                if (trimmedLine.StartsWith("*") || trimmedLine.StartsWith("-"))
                {
                    if (!inList)
                    {
                        htmlBuilder.AppendLine("    <ul>");
                        inList = true;
                    }
                    string itemText = trimmedLine.Substring(1).Trim();
                    htmlBuilder.AppendLine($"        <li>{System.Net.WebUtility.HtmlEncode(itemText)}</li>");
                    continue;
                }
                else
                {
                    if (inList)
                    {
                        htmlBuilder.AppendLine("    </ul>");
                        inList = false;
                    }
                }

                // Sayı ile başlayan satırlar (örn: 1. DÖNEM) alt başlıktır (H2)
                bool isHeader = false;
                if (trimmedLine.Length > 2)
                {
                    int dotIdx = trimmedLine.IndexOf('.');
                    if (dotIdx > 0 && dotIdx < 5)
                    {
                        string prefix = trimmedLine.Substring(0, dotIdx);
                        if (prefix.All(char.IsLetterOrDigit))
                        {
                            isHeader = true;
                        }
                    }
                }

                if (isHeader)
                {
                    htmlBuilder.AppendLine($"    <h2>{System.Net.WebUtility.HtmlEncode(trimmedLine)}</h2>");
                    continue;
                }

                // DİKKAT: veya UYARI: ile başlayanlar kırmızı uyarı kutusuna alınır
                if (trimmedLine.StartsWith("DİKKAT:", System.StringComparison.OrdinalIgnoreCase) || 
                    trimmedLine.StartsWith("UYARI:", System.StringComparison.OrdinalIgnoreCase))
                {
                    int colonIdx = trimmedLine.IndexOf(':');
                    string title = trimmedLine.Substring(0, colonIdx + 1);
                    string message = trimmedLine.Substring(colonIdx + 1).Trim();
                    htmlBuilder.AppendLine("    <div class='card card-warning'>");
                    htmlBuilder.AppendLine($"        <div class='card-warning-title'>⚠️ {System.Net.WebUtility.HtmlEncode(title)}</div>");
                    htmlBuilder.AppendLine($"        <div>{System.Net.WebUtility.HtmlEncode(message)}</div>");
                    htmlBuilder.AppendLine("    </div>");
                    continue;
                }

                // Sistem Davranışı: veya İpucu: ile başlayanlar mavi bilgi kutusuna alınır
                if (trimmedLine.StartsWith("Sistem Davranışı:", System.StringComparison.OrdinalIgnoreCase) || 
                    trimmedLine.StartsWith("İpucu:", System.StringComparison.OrdinalIgnoreCase))
                {
                    int colonIdx = trimmedLine.IndexOf(':');
                    string title = trimmedLine.Substring(0, colonIdx + 1);
                    string message = trimmedLine.Substring(colonIdx + 1).Trim();
                    htmlBuilder.AppendLine("    <div class='card card-info'>");
                    htmlBuilder.AppendLine($"        <div class='card-info-title'>ℹ️ {System.Net.WebUtility.HtmlEncode(title)}</div>");
                    htmlBuilder.AppendLine($"        <div>{System.Net.WebUtility.HtmlEncode(message)}</div>");
                    htmlBuilder.AppendLine("    </div>");
                    continue;
                }

                // Düz paragraf
                htmlBuilder.AppendLine($"    <p>{System.Net.WebUtility.HtmlEncode(trimmedLine)}</p>");
            }

            if (inList)
            {
                htmlBuilder.AppendLine("    </ul>");
            }

            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return htmlBuilder.ToString();
        }

        // ── Lisans Şifreli Gösterim İşlemleri ────────────────────────────────────

        private void BtnShowLicense_Click(object sender, RoutedEventArgs e)
        {
            if (BtnShowLicense.Content.ToString() == "Gizle")
            {
                TxtLicenseKey.Text = "••••-••••-••••-••••";
                BlurEffectLicense.Radius = 5;
                BtnShowLicense.Content = "Göster";
            }
            else
            {
                TxtLicenseVerifyPassword.Password = "";
                TxtLicenseVerifyPasswordError.Visibility = Visibility.Collapsed;
                LicenseVerifyOverlay.Visibility = Visibility.Visible;
                TxtLicenseVerifyPassword.Focus();
            }
        }

        private void BtnCancelLicenseVerify_Click(object sender, RoutedEventArgs e)
        {
            LicenseVerifyOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmLicenseVerify_Click(object sender, RoutedEventArgs e)
        {
            if (AuthService.CurrentUser == null) return;

            string password = TxtLicenseVerifyPassword.Password;
            if (string.IsNullOrEmpty(password))
            {
                TxtLicenseVerifyPasswordError.Text = "Lütfen şifrenizi girin.";
                TxtLicenseVerifyPasswordError.Visibility = Visibility.Visible;
                return;
            }

            if (SecurityHelper.VerifyPassword(password, AuthService.CurrentUser.PasswordHash))
            {
                TxtLicenseKey.Text = AuthService.CurrentUser.LicenseKey;
                BlurEffectLicense.Radius = 0;
                BtnShowLicense.Content = "Gizle";
                LicenseVerifyOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtLicenseVerifyPasswordError.Text = "Hatalı şifre. Lütfen tekrar deneyin.";
                TxtLicenseVerifyPasswordError.Visibility = Visibility.Visible;
            }
        }

        private void TxtLicenseVerifyPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnConfirmLicenseVerify_Click(sender, e);
            }
        }


        // ── Gelişmiş Sözleşme Geçmişi ve Onay Durumu İşlemleri ─────────────────

        private List<UserAgreementStatus> _allAgreementStatuses = new List<UserAgreementStatus>();

        private async void LoadAgreementsHistoryAndConsents()
        {
            try
            {
                var agreements = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    return db.Agreements.OrderByDescending(a => a.Id).ToList();
                });

                var latestAgreementId = agreements.FirstOrDefault()?.Id ?? 0;
                var wrapped = agreements.Select(a => new AgreementListItem
                {
                    Id = a.Id,
                    Version = a.Version,
                    Content = a.Content,
                    CreatedAt = a.CreatedAt,
                    IsLatest = a.Id == latestAgreementId
                }).ToList();

                LstAgreements.ItemsSource = wrapped;

                if (wrapped.Count > 0)
                {
                    LstAgreements.SelectedIndex = 0;
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Sözleşmeler yüklenirken hata: {ex.Message}");
            }
        }

        private void LstAgreements_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstAgreements.SelectedItem is AgreementListItem selected)
            {
                TxtSelectedVersionTitle.Text = $"Sözleşme Detayları ({selected.Version})";
                TxtSelectedVersionSub.Text = $"Yayınlanma: {selected.CreatedAt:dd.MM.yyyy HH:mm}";
                TxtSelectedAgreementContent.Text = selected.Content;

                LoadConsentsForAgreement(selected.Id, selected.Version);
            }
            else
            {
                TxtSelectedVersionTitle.Text = "Sözleşme Detayları";
                TxtSelectedVersionSub.Text = "Seçilen sürüm metni.";
                TxtSelectedAgreementContent.Text = string.Empty;
                GridAgreementConsents.ItemsSource = null;
                TxtAgreementAcceptedCount.Text = "Onay: 0";
                TxtAgreementPendingCount.Text = "Bekliyor: 0";
            }
        }

        private async void LoadConsentsForAgreement(int agreementId, string version)
        {
            try
            {
                var statuses = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var users = db.Users.ToList();
                    var consents = db.UserConsents.Where(c => c.AgreementId == agreementId).ToList();

                    var list = new List<UserAgreementStatus>();
                    foreach (var user in users)
                    {
                        var userConsent = consents.FirstOrDefault(c => c.UserId == user.Id);
                        bool isAccepted = userConsent != null;

                        list.Add(new UserAgreementStatus
                        {
                            Username = user.Username,
                            DealerName = user.DealerName,
                            Email = user.Email,
                            Role = user.Role,
                            AcceptedVersion = isAccepted ? version : "Yok",
                            IsAcceptedLatest = isAccepted ? "Kabul Etti" : "Kabul Etmedi",
                            AcceptedAtString = userConsent != null ? userConsent.AcceptedAt.ToString("dd.MM.yyyy HH:mm") : "-",
                            IpAddress = userConsent != null ? userConsent.IpAddress : "-",
                            IsAccepted = isAccepted
                        });
                    }
                    return list;
                });

                _allAgreementStatuses = statuses;
                FilterAndApplyStatuses();
            }
            catch (System.Exception ex)
            {
                ShowError($"Onay durumları yüklenirken hata: {ex.Message}");
            }
        }

        private void FilterAndApplyStatuses()
        {
            string searchText = TxtAgreementConsentSearch.Text.Trim().ToLowerInvariant();
            var filtered = _allAgreementStatuses;
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = _allAgreementStatuses.Where(s =>
                    s.Username.ToLowerInvariant().Contains(searchText) ||
                    s.DealerName.ToLowerInvariant().Contains(searchText)
                ).ToList();
            }

            GridAgreementConsents.ItemsSource = filtered;

            int acceptedCount = _allAgreementStatuses.Count(s => s.IsAccepted);
            int pendingCount = _allAgreementStatuses.Count(s => !s.IsAccepted);

            TxtAgreementAcceptedCount.Text = acceptedCount.ToString();
            TxtAgreementPendingCount.Text = pendingCount.ToString();
        }

        private void TxtAgreementConsentSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAndApplyStatuses();
        }

        // ─── Şifre Doğrulama İşlemleri (Ayarlar) ─────────────────────────────
        private void TxtNewPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidateSetPassword(TxtNewPassword.Password);
        }

        private void ValidateSetPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                SetRuleNeutral(IconSetLength, TxtSetLength);
                SetRuleNeutral(IconSetUpper, TxtSetUpper);
                SetRuleNeutral(IconSetLower, TxtSetLower);
                SetRuleNeutral(IconSetSymbol, TxtSetSymbol);
                return;
            }

            bool isLengthValid = password.Length >= 8;
            bool isUpperValid = password.Any(char.IsUpper);
            bool isLowerValid = password.Any(char.IsLower);
            bool isSymbolValid = password.Any(c => !char.IsLetterOrDigit(c));

            UpdateRuleUI(IconSetLength, TxtSetLength, isLengthValid);
            UpdateRuleUI(IconSetUpper, TxtSetUpper, isUpperValid);
            UpdateRuleUI(IconSetLower, TxtSetLower, isLowerValid);
            UpdateRuleUI(IconSetSymbol, TxtSetSymbol, isSymbolValid);
        }

        private void SetRuleNeutral(MaterialDesignThemes.Wpf.PackIcon icon, TextBlock text)
        {
            if (icon == null || text == null) return;
            icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        }

        private void UpdateRuleUI(MaterialDesignThemes.Wpf.PackIcon icon, TextBlock text, bool isValid)
        {
            if (icon == null || text == null) return;
            if (isValid)
            {
                icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#047857"));
            }
            else
            {
                icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CloseCircleOutline;
                icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
            }
        }
    }

    public class AgreementListItem
    {
        public int Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsLatest { get; set; }
    }

    public class UserAgreementStatus
    {
        public string Username { get; set; } = string.Empty;
        public string DealerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AcceptedVersion { get; set; } = "Yok";
        public string IsAcceptedLatest { get; set; } = "Kabul Etti";
        public string AcceptedAtString { get; set; } = "-";
        public string IpAddress { get; set; } = "-";
        public bool IsAccepted { get; set; }
    }
}
