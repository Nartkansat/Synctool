using Synctool.Data;
using Synctool.Services;
using Synctool.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Synctool.Views
{
    public partial class RegisterWindow : Window
    {
        /// <summary>
        /// Kayıt akışında kullanılır. Pencere kapandıktan sonra
        /// LoginWindow bu değeri okuyarak sözleşme onayı verilip verilmediğini anlar.
        /// </summary>
        public bool AgreementAccepted { get; private set; } = false;

        private int _currentAgreementId = 0;
        private User? _enforcedUser = null;
        private bool _isEnforcedAgreement = false;

        /// <summary>
        /// Normal kayıt akışı: kullanıcı "Kayıt Ol" linkine tıkladı.
        /// </summary>
        public RegisterWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Zorunlu sözleşme onayı akışı: giriş yapan kullanıcının
        /// yeni sözleşmeyi onaylaması gerekiyor.
        /// </summary>
        public RegisterWindow(User? enforcedUser, int agreementId)
        {
            InitializeComponent();
            _enforcedUser = enforcedUser;
            _currentAgreementId = agreementId;
            _isEnforcedAgreement = true;

            // Sadece sözleşmeyi göster, form alanları anlamsız
            // Pencere açılır açılmaz sözleşmeyi göster
            this.Loaded += (s, e) => LnkAgreement_Click(null, null);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isEnforcedAgreement)
            {
                // Zorunlu sözleşme reddedildi — logout
                AuthService.Logout();
            }
            this.Close();
        }

        private void BtnSwitchToLogin_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ─── Kayıt İşlemi ────────────────────────────────────────────────────
        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtRegUsername.Text.Trim();
            string dealerName = TxtRegDealerName.Text.Trim();
            string email = TxtRegEmail.Text.Trim();
            string password = TxtRegPasswordVisible.IsVisible ? TxtRegPasswordVisible.Text : TxtRegPassword.Password;
            string passwordConfirm = TxtRegPasswordConfirmVisible.IsVisible ? TxtRegPasswordConfirmVisible.Text : TxtRegPasswordConfirm.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(dealerName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordConfirm))
            {
                ShowRegError("Lütfen tüm alanları doldurun.");
                return;
            }

            if (password != passwordConfirm)
            {
                ShowRegError("Şifreler uyuşmuyor.");
                return;
            }

            if (password.Length < 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(c => !char.IsLetterOrDigit(c)))
            {
                ShowRegError("Şifre en az 8 karakter olmalı, en az 1 büyük harf, 1 küçük harf ve 1 özel karakter (sembol) içermelidir.");
                return;
            }

            if (ChkAgreement.IsChecked != true || _currentAgreementId == 0)
            {
                ShowRegError("Kayıt olmak için kullanıcı sözleşmesini okuyup kabul etmelisiniz.");
                return;
            }

            BtnRegister.IsEnabled = false;
            ErrorBorder.Visibility = Visibility.Collapsed;

            var result = await Task.Run(() => AuthService.Register(username, dealerName, email, password, _currentAgreementId));

            BtnRegister.IsEnabled = true;

            if (result.Success)
            {
                await ShowToast("Kayıt başarılı! Giriş yaparak lisansınızı etkinleştirebilirsiniz.");
                ClearRegForm();
            }
            else
            {
                ShowRegError(result.Message);
            }
        }

        private void ClearRegForm()
        {
            TxtRegUsername.Text = "";
            TxtRegDealerName.Text = "";
            TxtRegEmail.Text = "";
            TxtRegPassword.Password = "";
            TxtRegPasswordVisible.Text = "";
            TxtRegPasswordConfirm.Password = "";
            TxtRegPasswordConfirmVisible.Text = "";
            ChkAgreement.IsChecked = false;
            ValidateRegPassword("");
        }

        private void ShowRegError(string message)
        {
            TxtRegError.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        // ─── Şifre Göster/Gizle ──────────────────────────────────────────────
        private void BtnRegShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtRegPassword.Visibility == Visibility.Visible)
            {
                TxtRegPasswordVisible.Text = TxtRegPassword.Password;
                TxtRegPassword.Visibility = Visibility.Collapsed;
                TxtRegPasswordVisible.Visibility = Visibility.Visible;
                IconRegShowPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
                IconRegShowPassword.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E02020"));
                TxtRegPasswordVisible.Focus();
                TxtRegPasswordVisible.CaretIndex = TxtRegPasswordVisible.Text.Length;
            }
            else
            {
                TxtRegPassword.Password = TxtRegPasswordVisible.Text;
                TxtRegPasswordVisible.Visibility = Visibility.Collapsed;
                TxtRegPassword.Visibility = Visibility.Visible;
                IconRegShowPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
                IconRegShowPassword.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                TxtRegPassword.Focus();
            }
        }

        private void BtnRegShowPasswordConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (TxtRegPasswordConfirm.Visibility == Visibility.Visible)
            {
                TxtRegPasswordConfirmVisible.Text = TxtRegPasswordConfirm.Password;
                TxtRegPasswordConfirm.Visibility = Visibility.Collapsed;
                TxtRegPasswordConfirmVisible.Visibility = Visibility.Visible;
                IconRegShowPasswordConfirm.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
                IconRegShowPasswordConfirm.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E02020"));
                TxtRegPasswordConfirmVisible.Focus();
                TxtRegPasswordConfirmVisible.CaretIndex = TxtRegPasswordConfirmVisible.Text.Length;
            }
            else
            {
                TxtRegPasswordConfirm.Password = TxtRegPasswordConfirmVisible.Text;
                TxtRegPasswordConfirmVisible.Visibility = Visibility.Collapsed;
                TxtRegPasswordConfirm.Visibility = Visibility.Visible;
                IconRegShowPasswordConfirm.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
                IconRegShowPasswordConfirm.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                TxtRegPasswordConfirm.Focus();
            }
        }

        // ─── Şifre Doğrulama ─────────────────────────────────────────────────
        private void TxtRegPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidateRegPassword(TxtRegPassword.Password);
        }

        private void TxtRegPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateRegPassword(TxtRegPasswordVisible.Text);
        }

        private void ValidateRegPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                SetRuleNeutral(IconRegLength, TxtRegLength);
                SetRuleNeutral(IconRegUpper, TxtRegUpper);
                SetRuleNeutral(IconRegLower, TxtRegLower);
                SetRuleNeutral(IconRegSymbol, TxtRegSymbol);
                return;
            }

            UpdateRuleUI(IconRegLength, TxtRegLength, password.Length >= 8);
            UpdateRuleUI(IconRegUpper, TxtRegUpper, password.Any(char.IsUpper));
            UpdateRuleUI(IconRegLower, TxtRegLower, password.Any(char.IsLower));
            UpdateRuleUI(IconRegSymbol, TxtRegSymbol, password.Any(c => !char.IsLetterOrDigit(c)));
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

        // ─── Kullanıcı Sözleşmesi ────────────────────────────────────────────
        private void ChkAgreement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            LnkAgreement_Click(null, null);
        }

        private void AgreementScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var sv = (ScrollViewer)sender;
            if (sv.ScrollableHeight <= 0 || sv.VerticalOffset >= sv.ScrollableHeight - 5)
            {
                BtnAcceptAgreement.IsEnabled = true;
            }
        }

        private async void LnkAgreement_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                var agreement = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    return db.Agreements.OrderByDescending(a => a.Id).FirstOrDefault();
                });

                if (agreement != null)
                {
                    _currentAgreementId = agreement.Id;
                    TxtAgreementVersion.Text = $"Kullanıcı Sözleşmesi ({agreement.Version})";
                    TxtAgreementContent.Text = agreement.Content;
                    
                    AgreementScrollViewer.ScrollToHome();
                    BtnAcceptAgreement.IsEnabled = false;

                    AgreementOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowRegError("Kullanıcı sözleşmesi sistemde bulunamadı.");
                }
            }
            catch (System.Exception ex)
            {
                ShowRegError($"Sözleşme yüklenirken hata: {ex.Message}");
            }
        }

        private void BtnCloseAgreement_Click(object sender, RoutedEventArgs e)
        {
            if (_isEnforcedAgreement)
            {
                // Zorunlu onay reddedildi
                AuthService.Logout();
                this.Close();
                return;
            }
            AgreementOverlay.Visibility = Visibility.Collapsed;
            ChkAgreement.IsChecked = false;
        }

        private async void BtnAcceptAgreement_Click(object sender, RoutedEventArgs e)
        {
            if (_isEnforcedAgreement && _enforcedUser != null)
            {
                // Giriş akışında zorunlu sözleşme onayı
                await Task.Run(() => AuthService.AcceptAgreement(_enforcedUser.Id, _currentAgreementId));
                AgreementAccepted = true;
                this.Close();
                return;
            }

            ChkAgreement.IsChecked = true;
            AgreementOverlay.Visibility = Visibility.Collapsed;
        }

        // ─── Toast ────────────────────────────────────────────────────────────
        private async Task ShowToast(string message)
        {
            TxtToastMessage.Text = message;
            ToastCard.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1, System.TimeSpan.FromMilliseconds(400));
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(400));

            ToastCard.BeginAnimation(OpacityProperty, fadeIn);
            ((TranslateTransform)ToastCard.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);

            await System.Threading.Tasks.Task.Delay(3000);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(400));
            var slideOut = new System.Windows.Media.Animation.DoubleAnimation(50, System.TimeSpan.FromMilliseconds(400));

            fadeOut.Completed += (s, e) => ToastCard.Visibility = Visibility.Collapsed;

            ToastCard.BeginAnimation(OpacityProperty, fadeOut);
            ((TranslateTransform)ToastCard.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideOut);
        }
    }
}
