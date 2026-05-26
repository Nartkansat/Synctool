using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Synctool.Views;
using Synctool.Services;
using Synctool.Services;
using Synctool.Models;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;

namespace Synctool
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _notificationPollTimer;
        private System.Collections.Generic.HashSet<int> _notifiedIds = new();
        
        // Session kontrolünü cache'le — her tıklamada DB'ye gitmesin
        private DateTime _lastSessionCheck = DateTime.MinValue;
        private bool _lastSessionValid = true;
        private static readonly TimeSpan SessionCheckInterval = TimeSpan.FromSeconds(30);

        public MainWindow()
        {
            InitializeComponent();

            // Uygulama versiyonunu göster
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                TxtAppVersion.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            // Giriş yapan kullanıcı bilgilerini göster
            if (AuthService.CurrentUser != null)
            {
                TxtUsername.Text = AuthService.CurrentUser.Username;
                TxtRole.Text = AuthService.CurrentUser.Role;

                // Admin değilse admin bölümünü gizle
                if (AuthService.CurrentUser.Role != "Admin")
                {
                    AdminSection.Visibility = Visibility.Collapsed;
                }
            }

            // Subscribe to notification changes
            NotificationService.NotificationsChanged += (s, e) => {
                Dispatcher.Invoke(() => _ = RefreshNotificationsAsync());
            };
            
            // Subscribe to Modern Dialog Service
            ModernDialogService.DialogRequested += ModernDialogService_DialogRequested;

            // Subscribe to Cart changes
            CartService.Instance.CartChanged += (s, e) => {
                Dispatcher.Invoke(() => RefreshCartUI());
            };
            RefreshCartUI();
            
            _ = RefreshNotificationsAsync();

            // Set default view by simulating a click on the Dashboard button
            Nav_Click(BtnDashboard, null);

            // Setup polling for new notifications (async)
            _notificationPollTimer = new DispatcherTimer();
            _notificationPollTimer.Interval = TimeSpan.FromSeconds(15);
            _notificationPollTimer.Tick += NotificationPollTimer_Tick;
            _notificationPollTimer.Start();
            
            // First run to mark existing as "already notified" so we don't spam on startup
            _ = InitNotificationCacheAsync();
        }

        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Buton işaretliyse (Açık Menü)
            if (MenuToggleButton.IsChecked == true)
            {
                SidebarColumn.Width = new GridLength(220);
            }
            // Buton işaretsizse (Kapalı/Dar Menü)
            else
            {
                SidebarColumn.Width = new GridLength(62);
            }
        }

        private async Task InitNotificationCacheAsync()
        {
            if (AuthService.CurrentUser != null)
            {
                int userId = AuthService.CurrentUser.Id;
                var currentNotifications = await Task.Run(() => NotificationService.GetUserNotifications(userId));
                foreach (var n in currentNotifications)
                {
                    _notifiedIds.Add(n.Id);
                }
            }
        }

        private async void NotificationPollTimer_Tick(object? sender, EventArgs e)
        {
            if (AuthService.CurrentUser == null) return;

            try
            {
                int userId = AuthService.CurrentUser.Id;
                var notifiedIdsCopy = new System.Collections.Generic.HashSet<int>(_notifiedIds);
                
                var unread = await Task.Run(() => 
                    NotificationService.GetUserNotifications(userId)
                        .Where(n => !n.IsRead && !notifiedIdsCopy.Contains(n.Id))
                        .ToList());

                if (unread.Any())
                {
                    foreach (var n in unread)
                    {
                        _notifiedIds.Add(n.Id);
                        NotificationService.ShowToast(n.Title, n.Message, n.Type);
                    }
                    await RefreshNotificationsAsync();
                }
            }
            catch { /* Ağ hatası olursa sessizce devam et */ }
        }

        #region Modern Dialog System
        private void ModernDialogService_DialogRequested(object? sender, ModernDialogEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                TxtDialogTitle.Text = e.Title;
                TxtDialogMessage.Text = e.Message;
                
                // Reset buttons
                BtnDialogCancel.Visibility = e.Type == ModernDialogType.Question ? Visibility.Visible : Visibility.Collapsed;
                BtnDialogConfirm.Content = e.Type == ModernDialogType.Question ? "Evet" : "Tamam";
                Grid.SetColumn(BtnDialogConfirm, e.Type == ModernDialogType.Question ? 1 : 0);
                Grid.SetColumnSpan(BtnDialogConfirm, e.Type == ModernDialogType.Question ? 1 : 2);

                // Set theme colors/icons
                switch (e.Type)
                {
                    case ModernDialogType.Success:
                        BorderDialogIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                        IconDialog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                        IconDialog.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                        BtnDialogConfirm.Background = IconDialog.Foreground;
                        BtnDialogConfirm.BorderBrush = IconDialog.Foreground;
                        break;
                    case ModernDialogType.Error:
                        BorderDialogIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"));
                        IconDialog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                        IconDialog.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertOctagon;
                        BtnDialogConfirm.Background = IconDialog.Foreground;
                        BtnDialogConfirm.BorderBrush = IconDialog.Foreground;
                        break;
                    case ModernDialogType.Warning:
                    case ModernDialogType.Question:
                        BorderDialogIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                        IconDialog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                        IconDialog.Kind = MaterialDesignThemes.Wpf.PackIconKind.Alert;
                        BtnDialogConfirm.Background = IconDialog.Foreground;
                        BtnDialogConfirm.BorderBrush = IconDialog.Foreground;
                        break;
                    default:
                        BorderDialogIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
                        IconDialog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                        IconDialog.Kind = MaterialDesignThemes.Wpf.PackIconKind.Information;
                        BtnDialogConfirm.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A24"));
                        BtnDialogConfirm.BorderBrush = BtnDialogConfirm.Background;
                        break;
                }

                ModernDialogOverlay.Visibility = Visibility.Visible;
            });
        }

        private void BtnDialogResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && bool.TryParse(btn.Tag?.ToString(), out bool result))
            {
                ModernDialogOverlay.Visibility = Visibility.Collapsed;
                ModernDialogService.SetResult(result);
            }
        }
        #endregion

        #region Notifications
        private async Task RefreshNotificationsAsync()
        {
            if (AuthService.CurrentUser == null) return;
            
            try
            {
                int userId = AuthService.CurrentUser.Id;
                
                var result = await Task.Run(() =>
                {
                    var list = NotificationService.GetUserNotifications(userId);
                    var unreadCount = NotificationService.GetUnreadCount(userId);
                    return (list, unreadCount);
                });

                BadgeNotifications.Badge = result.unreadCount > 0 ? (object)result.unreadCount : null;
                ItemsNotifications.ItemsSource = result.list.Take(5);

                // Sidebar notification badge güncelle
                if (result.unreadCount > 0)
                {
                    var countStr = result.unreadCount > 99 ? "99+" : result.unreadCount.ToString();
                    TxtSidebarNotifBadgeExpanded.Text = countStr;
                    SidebarNotifBadgeExpanded.Visibility = Visibility.Visible;
                }
                else
                {
                    SidebarNotifBadgeExpanded.Visibility = Visibility.Collapsed;
                }
            }
            catch { /* Ağ hatası olursa UI'ı çökertme */ }
        }

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            PopupNotifications.IsOpen = !PopupNotifications.IsOpen;
        }

        private async void BtnMarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            if (AuthService.CurrentUser != null)
            {
                var result = await ModernDialogService.ShowAsync("Tümünü Okundu İşaretle",
                   "Tüm bildirimleri okundu olarak işaretlemek istediğinize emin misiniz?",
                   ModernDialogType.Question);

                if (result)
                {
                    int userId = AuthService.CurrentUser.Id;
                    await Task.Run(() => NotificationService.MarkAllAsRead(userId));
                    _ = ModernDialogService.ShowAsync("Başarılı", "Tüm bildirimler okundu olarak işaretlendi.", ModernDialogType.Success);
                }
            }
        }

        private void BtnNotificationDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                _ = Task.Run(() => NotificationService.MarkAsRead(id));
                PopupNotifications.IsOpen = false;
                
                _ = Task.Run(() =>
                {
                    using var db = new Synctool.Data.AppDbContext();
                    var notification = db.Notifications.Find(id);
                    if (notification != null)
                    {
                        var type = notification.Type switch
                        {
                            "Success" => ModernDialogType.Success,
                            "Warning" => ModernDialogType.Warning,
                            "Error" => ModernDialogType.Error,
                            _ => ModernDialogType.Info
                        };

                        Dispatcher.Invoke(() => _ = ModernDialogService.ShowAsync(notification.Title, notification.Message, type));
                    }
                });
            }
        }

        private void BtnViewAllNotifications_Click(object sender, RoutedEventArgs e)
        {
            PopupNotifications.IsOpen = false;
            SetPageHeader("Tüm Bildirimler", "Sistem bildirimleri ve duyurular", PackIconKind.BellRing, "#F59E0B", "#FFFBEB");
            MainContentControl.Content = new NotificationsView();
        }
        #endregion

        #region Navigation
        private void ResetNavButtons(Panel parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is Button menuBtn)
                {
                    menuBtn.ClearValue(Button.BackgroundProperty);
                    menuBtn.ClearValue(Button.ForegroundProperty);
                    menuBtn.ClearValue(Button.BorderBrushProperty);
                    menuBtn.ClearValue(UIElement.EffectProperty);
                }
                else if (child is Panel panel)
                {
                    ResetNavButtons(panel);
                }
            }
        }

        private async void Nav_Click(object sender, RoutedEventArgs e)
        {
            // Session kontrolünü cache'le — her tıklamada DB'ye gitmesin
            if (DateTime.Now - _lastSessionCheck > SessionCheckInterval)
            {
                try
                {
                    _lastSessionValid = await Task.Run(() => AuthService.IsSessionValid());
                    _lastSessionCheck = DateTime.Now;
                }
                catch
                {
                    // Ağ hatası — son durumu koru
                }
            }

            if (!_lastSessionValid)
            {
                _ = ModernDialogService.ShowAsync("Oturum Hatası", "Oturumunuz sonlandırıldı. Başka bir cihazdan giriş yapılmış olabilir.", ModernDialogType.Error);
                BtnLogout_Click(null, null);
                return;
            }

            if (sender is Button btn && btn.Tag is string tag)
            {
                if (MenuStackPanel != null)
                {
                    ResetNavButtons(MenuStackPanel);
                }

                btn.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x20));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x20, 0x20));
                btn.Foreground = Brushes.White;
                PerformNavigation(tag);
            }
        }

        public void NavigateToPage(string tag, object parameter = null)
        {
            Button targetBtn = FindButtonByTag(MenuStackPanel, tag);
            if (targetBtn != null)
            {
                ResetNavButtons(MenuStackPanel);
                targetBtn.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x20));
                targetBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x20, 0x20));
                targetBtn.Foreground = Brushes.White;
                PerformNavigation(tag, parameter);
            }
        }

        private Button FindButtonByTag(Panel parent, string tag)
        {
            foreach (var child in parent.Children)
            {
                if (child is Button btn && btn.Tag as string == tag) return btn;
                if (child is Panel panel)
                {
                    var found = FindButtonByTag(panel, tag);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void SetPageHeader(string title, string subtitle, PackIconKind iconKind, string iconColor, string iconBg)
        {
            TxtPageTitle.Text = title;
            TxtPageSubtitle.Text = subtitle;
            IconPageTitle.Kind = iconKind;
            IconPageTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconColor));
            BorderPageIcon.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBg));
        }

        private void PerformNavigation(string tag, object parameter = null)
        {
            switch (tag)
                {
                    case "Anasayfa":
                        SetPageHeader("Dashboard", "Genel istatistikler ve sistem özeti", PackIconKind.ViewDashboard, "#4F46E5", "#EEF2FF");
                        MainContentControl.Content = new DashboardView();
                        break;
                    case "Kea":
                        SetPageHeader("Küçük Ev Aletleri", "KEA maliyet listesi ve fiyat analizleri", PackIconKind.Blender, "#E02020", "#FFF1F2");
                        MainContentControl.Content = new KeaView();
                        break;
                    case "BeyazEsya":
                        SetPageHeader("Beyaz Eşya", "Beyaz eşya maliyet listesi ve fiyat analizleri", PackIconKind.WashingMachine, "#3B82F6", "#EFF6FF");
                        MainContentControl.Content = new BeyazEsyaView();
                        break;
                    case "Tarihce":
                        SetPageHeader("Eski Fiyat Arşivi", "Geçmiş dönem fiyat kayıtları", PackIconKind.History, "#F59E0B", "#FFFBEB");
                        if (parameter is string group)
                            MainContentControl.Content = new TarihceView(group);
                        else
                            MainContentControl.Content = new TarihceView();
                        break;

                    case "YeniFiyat":
                        SetPageHeader("Maliyet Hesaplama", "Anlık fiyat ve maliyet hesaplama aracı", PackIconKind.CalculatorVariant, "#10B981", "#ECFDF5");
                        MainContentControl.Content = new YeniFiyatView();
                        break;
                    case "ExcelViewer":
                        SetPageHeader("Excel Görüntüleyici", "Yüklenen Excel dosyalarını görüntüle", PackIconKind.MicrosoftExcel, "#16A34A", "#F0FDF4");
                        MainContentControl.Content = new ExcelViewer();
                        break;
                    case "ManualCampaign":
                        if (AuthService.CurrentUser?.Role != "Admin")
                        {
                            _ = ModernDialogService.ShowAsync("Yetki Hatası", "Bu bölüme sadece yöneticiler erişebilir.", ModernDialogType.Warning);
                            return;
                        }
                        SetPageHeader("Kampanya Yönetimi", "Manuel kampanya tanımları ve yönetimi", PackIconKind.TagPlus, "#E11D48", "#FFF1F2");
                        MainContentControl.Content = new ManualCampaignView();
                        break;
                    case "UserManagement":
                        if (AuthService.CurrentUser?.Role != "Admin")
                        {
                            _ = ModernDialogService.ShowAsync("Yetki Hatası", "Bu bölüme sadece yöneticiler erişebilir.", ModernDialogType.Warning);
                            return;
                        }
                        SetPageHeader("Kullanıcı Yönetimi", "Sistem kullanıcıları ve yetki yönetimi", PackIconKind.AccountGroup, "#8B5CF6", "#F5F3FF");
                        MainContentControl.Content = new UserManagementView();
                        break;
                    case "Excell":
                        if (AuthService.CurrentUser?.Role != "Admin")
                        {
                            _ = ModernDialogService.ShowAsync("Yetki Hatası", "Bu bölüme sadece yöneticiler erişebilir.", ModernDialogType.Warning);
                            return;
                        }
                        SetPageHeader("Excel İçeri Aktar", "Fiyat listelerini sisteme aktar", PackIconKind.DatabaseImport, "#0EA5E9", "#F0F9FF");
                        MainContentControl.Content = new ExcelIslemleriView();
                        break;
                    case "FileManagement":
                        if (AuthService.CurrentUser?.Role != "Admin")
                        {
                            _ = ModernDialogService.ShowAsync("Yetki Hatası", "Bu bölüme sadece yöneticiler erişebilir.", ModernDialogType.Warning);
                            return;
                        }
                        SetPageHeader("Dosya Yönetimi", "Yüklenen Excel dosyalarını yönet", PackIconKind.FileCog, "#64748B", "#F1F5F9");
                        MainContentControl.Content = new FileManagementView();
                        break;
                    case "Settings":
                        SetPageHeader("Ayarlar", "Profil ve sistem ayarları", PackIconKind.CogOutline, "#475569", "#F8FAFC");
                        MainContentControl.Content = new SettingsView();
                        break;
                    case "Notifications":
                        SetPageHeader("Tüm Bildirimler", "Sistem bildirimleri ve duyurular", PackIconKind.BellRing, "#F59E0B", "#FFFBEB");
                        MainContentControl.Content = new NotificationsView();
                        break;
                    case "KampanyaBilgi":
                        SetPageHeader("Kampanya Bilgi", "Kampanya detayları ve bilgilendirmeler", PackIconKind.InformationOutline, "#0891B2", "#ECFEFF");
                        MainContentControl.Content = new KampanyaBilgiView();
                        break;
                    case "KampanyaDegisim":
                        SetPageHeader("Kampanya Değişim", "İki Excel listesi arasındaki fiyat değişimlerini analiz et", PackIconKind.FileReplaceOutline, "#7C3AED", "#F5F3FF");
                        MainContentControl.Content = new KampanyaDegisimView();
                        break;
                    case "StokMaliyet":
                        SetPageHeader("Stok Maliyet Analizi", "Excel dosyası üzerinden stok maliyetlerini hesaplayın", PackIconKind.FilePercent, "#0EA5E9", "#F0F9FF");
                        MainContentControl.Content = new StokMaliyetView();
                        break;
                }
        }


        #region Toast Logic
        private int _toastId = 0;

        public async void ShowModernToast(string message)
        {
            int currentId = ++_toastId;

            Dispatcher.Invoke(() =>
            {
                TxtToastMessage.Text = message;
                
                // Animasyonlar - Giriş
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
                var slideUp = new System.Windows.Media.Animation.DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(500))
                {
                    EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.3, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                ToastOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
            });

            // 5 saniye bekle
            await Task.Delay(5000);

            // Eğer bu bekleme sırasında yeni bir toast çağrılmadıysa çıkış animasyonunu oynat
            if (currentId != _toastId) return;

            Dispatcher.Invoke(() =>
            {
                // Animasyonlar - Çıkış
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
                var slideDown = new System.Windows.Media.Animation.DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(400));

                ToastOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideDown);
            });
        }
        #endregion

        #region Cart Logic
        private decimal _cartDiscount = 0;

        private void TxtCartDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(TxtCartDiscount.Text, out decimal discount))
            {
                _cartDiscount = discount;
            }
            else
            {
                _cartDiscount = 0;
            }
            RefreshCartUI();
        }

        private void RefreshCartUI()
        {
            var cart = CartService.Instance;
            BadgeCart.Badge = cart.Count > 0 ? (object)cart.Count : null;
            ItemsCart.ItemsSource = null;
            ItemsCart.ItemsSource = cart.Items;

            decimal subTotal = cart.Items.Sum(i => i.SelectedPrice);
            
            decimal finalTotal = subTotal - _cartDiscount;
            if (finalTotal < 0) finalTotal = 0;

            decimal finalTotalCard = 0;

            if (subTotal > 0)
            {
                foreach (var item in cart.Items)
                {
                    // İndirimi orantısal dağıt
                    decimal ratio = item.SelectedPrice / subTotal;
                    decimal itemDiscount = _cartDiscount * ratio;
                    
                    decimal discountedItemPrice = item.SelectedPrice - itemDiscount;
                    if (discountedItemPrice < 0) discountedItemPrice = 0;

                    // Her bir ürün için güncel iskontolu fiyatın üzerine kart vade farkını ekle
                    finalTotalCard += discountedItemPrice * (1 + item.CardMarkupPercent / 100m);
                }
            }

            if (TxtCartSubTotal != null)
                TxtCartSubTotal.Text = $"{subTotal:N2} ₺";
            if (TxtCartTotal != null)
                TxtCartTotal.Text = $"{finalTotal:N2} ₺";
            if (TxtCartTotalCard != null)
                TxtCartTotalCard.Text = $"{finalTotalCard:N2} ₺";
        }

        private void BtnCart_Click(object sender, RoutedEventArgs e)
        {
            PopupCart.IsOpen = !PopupCart.IsOpen;
        }

        private void BtnRemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                CartService.Instance.RemoveItem(id);
            }
        }

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            CartService.Instance.Clear();
            if (TxtCartDiscount != null) TxtCartDiscount.Text = string.Empty;
            _cartDiscount = 0;
            PopupCart.IsOpen = false;
            ShowModernToast("Sepet temizlendi.");
        }

        #endregion

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _ = Task.Run(() => AuthService.Logout());
            LoginWindow login = new LoginWindow();
            login.Visibility = Visibility.Visible;
            login.Show();
            this.Close();
        }
        #endregion
    }
}
