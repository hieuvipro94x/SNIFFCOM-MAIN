using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace SniffCom
{
    public partial class App : WpfApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += App_DispatcherUnhandledException;

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Start(e.Args.Any(arg => string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase)));
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(
                $"Ung dung gap loi: {e.Exception.Message}",
                "SniffCom",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}

