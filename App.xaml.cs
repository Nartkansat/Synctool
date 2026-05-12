using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ArcelikExcelApp.Views;
using ArcelikApp.Services;

namespace ArcelikExcelApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string WampProcessName = "wampmanager";
    private const string WampExePath     = @"C:\wamp64\wampmanager.exe";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Güncelleme kontrolünü başlat (fire-and-forget)
        // CheckForUpdateEvent içinde güncelleme varsa UpdateRequired = true yapılır
        // ve açık pencereler kapatılır
        try { UpdateService.CheckForUpdates(); } catch { }

        // 2. WAMP kontrolünü arka planda başlat (UI'ı bloke etme)
        _ = Task.Run(() => EnsureWampRunningAsync());

        // 3. Auto-login kontrolü — bu süre zarfında (genellikle 1-3 sn)
        //    güncelleme HTTP isteği de tamamlanmış olur
        bool autoLoginSuccess = false;
        try
        {
            autoLoginSuccess = await ArcelikApp.Services.AuthService.CheckAutoLoginAsync();
        }
        catch { }

        // 4. Güncelleme gerekiyorsa pencere açma — AutoUpdater Forced dialog'unu
        //    zaten gösteriyor, ShutdownMode OnCheckForUpdate içinde ayarlandı
        if (UpdateService.UpdateRequired)
            return;

        // 5. Normal akış
        if (autoLoginSuccess && AuthService.CurrentUser != null)
        {
            if (AuthService.MustAcceptLatestAgreement(AuthService.CurrentUser.Id))
            {
                AuthService.Logout();
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }
            else
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }
        else
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }

    private static async Task EnsureWampRunningAsync()
    {
        try
        {
            bool calisiyorMu = Process.GetProcesses()
                .Any(p => p.ProcessName.Equals(WampProcessName, StringComparison.OrdinalIgnoreCase));

            if (calisiyorMu)
            {
                Debug.WriteLine($"{WampProcessName} zaten çalışıyor.");
                return;
            }

            if (!File.Exists(WampExePath))
            {
                Debug.WriteLine($"WampServer bulunamadı: {WampExePath}");
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName        = WampExePath,
                    UseShellExecute = true,
                    Verb            = "runas"
                };
                Process.Start(startInfo);
                Debug.WriteLine($"{WampProcessName} başlatıldı.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"WampManager başlatılamadı: {ex.Message}");
                return;
            }

            await WaitForProcessAsync(WampProcessName, timeoutSeconds: 10);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EnsureWampRunning hatası: {ex.Message}");
        }
    }

    private static async Task WaitForProcessAsync(string processName, int timeoutSeconds)
    {
        int elapsed = 0;
        const int interval = 1000;
        int maxMs = timeoutSeconds * 1000;

        while (elapsed < maxMs)
        {
            bool running = Process.GetProcesses()
                .Any(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

            if (running) return;

            await Task.Delay(interval);
            elapsed += interval;
        }

        Debug.WriteLine($"{processName} {timeoutSeconds} saniye içinde başlamadı, devam ediliyor.");
    }
}
