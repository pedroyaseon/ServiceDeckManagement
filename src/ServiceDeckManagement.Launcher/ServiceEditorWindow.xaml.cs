using System.Text.RegularExpressions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Launcher;

public partial class ServiceEditorWindow : Window
{
    private static readonly Regex ServiceIdPattern = new(
        "^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private readonly string productRoot;
    private readonly ServiceDefinitionV1? original;

    public ServiceEditorWindow(string productRoot, ServiceDefinitionV1? definition, bool browseExistingApplication)
    {
        InitializeComponent();
        this.productRoot = Path.GetFullPath(productRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        original = definition;
        LoadDefinition(definition);
        if (browseExistingApplication) ContentRendered += BrowseOnOpen;
    }

    public ServiceDefinitionV1? Definition { get; private set; }

    private void LoadDefinition(ServiceDefinitionV1? definition)
    {
        EditorTitle.Text = definition is null ? "Adicionar serviço" : "Editar serviço";
        IdTextBox.IsEnabled = definition is null;
        IdTextBox.Text = definition?.Id ?? string.Empty;
        DisplayNameTextBox.Text = definition?.DisplayName ?? string.Empty;
        ExecutableTextBox.Text = definition?.Executable ?? string.Empty;
        WorkingDirectoryTextBox.Text = definition?.WorkingDirectory ?? string.Empty;
        ArgumentsTextBox.Text = definition is null ? string.Empty : string.Join(Environment.NewLine, definition.Arguments);
        StartModeComboBox.SelectedValue = definition?.StartMode ?? "manual";
        RestartEnabledCheckBox.IsChecked = definition?.RestartPolicy.Enabled ?? true;
        LoggingEnabledCheckBox.IsChecked = definition?.Logging.Enabled ?? true;
        HealthTypeComboBox.SelectedValue = definition?.HealthCheck.Type ?? "process";
        HealthTargetTextBox.Text = definition?.HealthCheck.Target ?? string.Empty;
        UpdateHealthTargetState();
    }

    private void BrowseOnOpen(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ContentRendered -= BrowseOnOpen;
        BrowseForExecutable();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BrowseForExecutable();
    }

    private void BrowseForExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar executável",
            Filter = "Aplicações Windows (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(Path.Combine(productRoot, "apps"))
                ? Path.Combine(productRoot, "apps")
                : productRoot
        };
        if (dialog.ShowDialog(this) != true) return;
        var fullPath = Path.GetFullPath(dialog.FileName);
        var relative = Path.GetRelativePath(productRoot, fullPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            ValidationText.Text = "O executável deve estar dentro da pasta portátil do produto.";
            return;
        }

        ExecutableTextBox.Text = NormalizePath(relative);
        WorkingDirectoryTextBox.Text = NormalizePath(Path.GetDirectoryName(relative) ?? string.Empty);
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(fullPath);
        }
        if (string.IsNullOrWhiteSpace(IdTextBox.Text))
        {
            IdTextBox.Text = CreateServiceId(Path.GetFileNameWithoutExtension(fullPath));
        }
        ValidationText.Text = string.Empty;
    }

    private void HealthTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (IsInitialized) UpdateHealthTargetState();
    }

    private void UpdateHealthTargetState()
    {
        var isProcess = string.Equals(HealthTypeComboBox.SelectedValue as string, "process", StringComparison.Ordinal);
        HealthTargetTextBox.IsEnabled = !isProcess;
        if (isProcess) HealthTargetTextBox.Clear();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var id = IdTextBox.Text.Trim();
        var displayName = DisplayNameTextBox.Text.Trim();
        var executable = NormalizePath(ExecutableTextBox.Text.Trim());
        var workingDirectory = NormalizePath(WorkingDirectoryTextBox.Text.Trim());
        var arguments = ArgumentsTextBox.Text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var startMode = StartModeComboBox.SelectedValue as string ?? "manual";
        var healthType = HealthTypeComboBox.SelectedValue as string ?? "process";
        var healthTarget = HealthTargetTextBox.Text.Trim();

        var error = Validate(id, displayName, executable, workingDirectory, arguments, healthType, healthTarget);
        if (error is not null)
        {
            ValidationText.Text = error;
            return;
        }

        var baseline = original ?? new ServiceDefinitionV1();
        Definition = baseline with
        {
            SchemaVersion = ContractVersions.ServiceDefinitionSchema,
            Id = id,
            DisplayName = displayName,
            Executable = executable,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            Environment = original?.Environment ?? new(StringComparer.OrdinalIgnoreCase),
            SecretReferences = original?.SecretReferences ?? new(StringComparer.OrdinalIgnoreCase),
            StartMode = startMode,
            RestartPolicy = baseline.RestartPolicy with { Enabled = RestartEnabledCheckBox.IsChecked == true },
            Logging = baseline.Logging with { Enabled = LoggingEnabledCheckBox.IsChecked == true },
            HealthCheck = baseline.HealthCheck with
            {
                Type = healthType,
                Target = healthType == "process" ? null : healthTarget
            }
        };
        DialogResult = true;
    }

    private string? Validate(
        string id,
        string displayName,
        string executable,
        string workingDirectory,
        string[] arguments,
        string healthType,
        string healthTarget)
    {
        if (!ServiceIdPattern.IsMatch(id)) return "Use um identificador com letras minúsculas, números e hífen.";
        if (displayName.Length is < 1 or > 128 || displayName.Any(char.IsControl)) return "Informe um nome válido.";
        if (!TryResolveInsideRoot(executable, requireFile: true)) return "O executável deve existir dentro da pasta do produto.";
        if (!TryResolveInsideRoot(workingDirectory, requireFile: false)) return "O diretório de trabalho deve existir dentro da pasta do produto.";
        if (arguments.Length > 128 || arguments.Any(value => value.Length > 4_096 || value.Any(char.IsControl))) return "Revise os argumentos informados.";
        if (healthType != "process" && string.IsNullOrWhiteSpace(healthTarget)) return "Informe o alvo do health check.";
        return null;
    }

    private bool TryResolveInsideRoot(string relativePath, bool requireFile)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return false;
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(productRoot, relativePath));
            var relative = Path.GetRelativePath(productRoot, fullPath);
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) return false;
            return requireFile ? File.Exists(fullPath) : Directory.Exists(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string CreateServiceId(string value)
    {
        var normalized = new string(value.ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        normalized = normalized.Trim('-');
        if (normalized.Length > 63) normalized = normalized[..63].TrimEnd('-');
        return normalized.Length == 0 ? "service" : normalized;
    }
}
