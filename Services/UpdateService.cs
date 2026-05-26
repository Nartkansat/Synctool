using System;
using AutoUpdaterDotNET;

namespace Synctool.Services
{
    public static class UpdateService
    {
        // GitHub deponuz Public olduğu için direkt raw URL kullanıyoruz.
        private const string UpdateXmlUrl = "https://raw.githubusercontent.com/nartkansat/Synctool/main/update.xml";

        public static void CheckForUpdates()
        {
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.DownloadPath = Environment.CurrentDirectory;
            
            try 
            {
                AutoUpdater.Start(UpdateXmlUrl);
            }
            catch (Exception)
            {
                // Hata durumunda sessiz kalabilir veya bildirim verebilirsiniz.
            }
        }
    }
}

