using System;
using System.Windows;
using AutoUpdaterDotNET;

namespace ArcelikApp.Services
{
    public static class UpdateService
    {
        private const string UpdateXmlUrl = "https://raw.githubusercontent.com/nartkansat/ArcelikApp/main/update.xml";

        // Güncelleme bulunduysa true olur — App.xaml.cs bu flag'i kontrol eder
        public static volatile bool UpdateRequired = false;

        public static void CheckForUpdates()
        {
            // Güncelleme sonucunu dinle
            AutoUpdater.CheckForUpdateEvent += OnCheckForUpdate;

            AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdater.ShowSkipButton        = false;
            AutoUpdater.RunUpdateAsAdmin      = true;
            AutoUpdater.Mandatory             = true;
            AutoUpdater.UpdateMode            = Mode.Forced;
            AutoUpdater.DownloadPath          = Environment.CurrentDirectory;

            try
            {
                AutoUpdater.Start(UpdateXmlUrl);
            }
            catch
            {
                AutoUpdater.CheckForUpdateEvent -= OnCheckForUpdate;
            }
        }

        private static void OnCheckForUpdate(UpdateInfoEventArgs args)
        {
            AutoUpdater.CheckForUpdateEvent -= OnCheckForUpdate;

            if (args.Error != null || !args.IsUpdateAvailable)
                return;

            // Güncelleme var — flag'i işaretle ve açık pencereleri kapat
            UpdateRequired = true;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // Açılmış olan tüm pencereleri kapat
                foreach (Window w in Application.Current.Windows)
                    w.Close();

                // WPF'nin "pencere yok = kapat" davranışını engelle;
                // AutoUpdater Mode.Forced kendi dialog'unu gösterecek ve
                // işlem bitince Environment.Exit() ile uygulamayı kapatacak
                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            });
        }
    }
}
