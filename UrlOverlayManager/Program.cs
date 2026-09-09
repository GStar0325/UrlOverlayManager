namespace UrlOverlayManager
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using Mutex singleInstanceMutex = new Mutex(true, SingleInstanceManager.MutexName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                SingleInstanceManager.NotifyExistingInstance();
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            UiSettings uiSettings = UiSettingsStore.Load();
            UiSettingsStore.Apply(uiSettings);

            try
            {
                Application.SetDefaultFont(UiTheme.RegularFont());
            }
            catch
            {
                Application.SetDefaultFont(SystemFonts.MessageBoxFont ?? new Font(FontFamily.GenericSansSerif, 9F));
            }

            BroadcastToolboxForm mainForm = new BroadcastToolboxForm();
            SingleInstanceManager.StartServer(mainForm.ShowMainForm);

            try
            {
                Application.Run(mainForm);
            }
            finally
            {
                SingleInstanceManager.StopServer();
                singleInstanceMutex.ReleaseMutex();
            }
        }
    }
}
