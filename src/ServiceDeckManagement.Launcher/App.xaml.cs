using System.Windows;
using System.IO;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
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
            var paths = new ProductPaths(root);
            var client = new NamedPipeManagerClient(new DpapiTransportKeyReader(paths));
            var setup = new ManagerSetupService(
                paths,
                new ElevatedSetupRunner(),
                new CurrentWindowsIdentity());
            var window = new MainWindow(
                new LocalManagerService(client),
                setup,
                root.RootPath);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ProductRootNotFoundException)
        {
            MessageBox.Show(
                "O Launcher não pôde validar a pasta local do produto.",
                "Service Deck Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
