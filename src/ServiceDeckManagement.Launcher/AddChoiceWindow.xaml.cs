using System.Windows;

namespace ServiceDeckManagement.Launcher;

public partial class AddChoiceWindow : Window
{
    public AddChoiceWindow() => InitializeComponent();

    public bool BrowseExistingApplication { get; private set; }

    private void NewDefinition_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BrowseExistingApplication = false;
        DialogResult = true;
    }

    private void ExistingApplication_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BrowseExistingApplication = true;
        DialogResult = true;
    }
}
