using System.Windows;
using System.IO;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Launcher;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var root = ProductRootLocator.FromApplicationBaseDirectory();
            var options = new LauncherConfiguration(new ProductPaths(root)).Load();
            var window = new MainWindow(options, root.RootPath);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ProductRootNotFoundException)
        {
            MessageBox.Show(
                "O Launcher não pôde validar a configuração local. Verifique a pasta do produto e config/launcher.json.",
                "Service Deck Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
