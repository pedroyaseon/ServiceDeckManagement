using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ServiceDeckManagement.Contracts.Manager;

namespace ServiceDeckManagement.Launcher;

public partial class MainWindow : Window
{
    private readonly LocalManagerService manager;
    private readonly ManagerSetupService setup;
    private readonly string productRoot;
    private readonly ObservableCollection<ManagedServiceSnapshotV1> services = [];
    private readonly List<ServiceLogEntryV1> logs = [];
    private readonly DispatcherTimer monitorTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool managerConnected;
    private bool operationInProgress;
    private bool monitorInProgress;
    private bool applyingInventory;
    private bool setupInProgress;
    private long logSequence;

    public MainWindow(
        LocalManagerService manager,
        ManagerSetupService setup,
        string productRoot)
    {
        InitializeComponent();
        this.manager = manager;
        this.setup = setup;
        this.productRoot = productRoot;
        ServicesList.ItemsSource = services;
        monitorTimer.Tick += MonitorTimer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private ManagedServiceSnapshotV1? SelectedService =>
        ServicesList.SelectedItem as ManagedServiceSnapshotV1;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await RefreshServicesAsync(CancellationToken.None);
        monitorTimer.Start();
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (monitorInProgress || operationInProgress) return;
        monitorInProgress = true;
        try
        {
            await RefreshServicesAsync(CancellationToken.None);
            await RefreshLogsAsync(CancellationToken.None);
        }
        finally
        {
            monitorInProgress = false;
        }
    }

    private async Task RefreshServicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var inventory = await manager.GetServicesAsync(cancellationToken);
            managerConnected = true;
            SetManagerStatus(online: true);
            ApplyInventory(inventory);
            SetFeedback(services.Count == 0
                ? "Manager conectado. Adicione uma aplicação para começar."
                : "Inventário local sincronizado.");
        }
        catch (LocalManagerException exception)
        {
            SetManagerUnavailable(exception.Message);
        }
        catch (InvalidDataException)
        {
            SetManagerUnavailable("O Manager local usa um protocolo incompatível com esta versão.");
        }
        catch (Exception exception) when (IsManagerUnavailable(exception))
        {
            SetManagerUnavailable(
                exception is UnauthorizedAccessException
                    ? "Este usuário do Windows não está autorizado no Manager local."
                    : "Manager local indisponível. Configure ou repare o componente local.");
        }
    }

    private void ApplyInventory(IReadOnlyList<ManagedServiceSnapshotV1> inventory)
    {
        var selectedId = SelectedService?.ServiceId;
        applyingInventory = true;
        try
        {
            services.Clear();
            foreach (var service in inventory.OrderBy(
                         item => item.DisplayName,
                         StringComparer.CurrentCultureIgnoreCase))
            {
                services.Add(service);
            }

            ServiceCountText.Text = services.Count switch
            {
                0 => "Nenhum serviço gerenciado",
                1 => "1 serviço gerenciado",
                _ => $"{services.Count} serviços gerenciados"
            };
            NoServicesPanel.Visibility = services.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SetupManagerButton.Visibility = Visibility.Collapsed;
            NoServicesTitle.Text = "Nenhum serviço adicionado";
            NoServicesDescription.Text = "Use Adicionar para registrar a primeira aplicação.";
            if (selectedId is not null)
            {
                ServicesList.SelectedItem = services.FirstOrDefault(
                    item => string.Equals(item.ServiceId, selectedId, StringComparison.Ordinal));
            }
        }
        finally
        {
            applyingInventory = false;
        }

        UpdateSelection();
    }

    private void SetManagerUnavailable(string message)
    {
        managerConnected = false;
        SetManagerStatus(online: false);
        applyingInventory = true;
        try
        {
            services.Clear();
            ServicesList.SelectedItem = null;
        }
        finally
        {
            applyingInventory = false;
        }

        logs.Clear();
        LogsTextBox.Text = "Configure o Manager local para carregar os logs.";
        ServiceCountText.Text = "Aguardando o Manager local";
        NoServicesPanel.Visibility = Visibility.Visible;
        NoServicesTitle.Text = "Manager local indisponível";
        NoServicesDescription.Text = setup.IsPackageComplete
            ? "Configure ou repare o componente local para continuar."
            : "O pacote está incompleto. Gere novamente a distribuição portátil.";
        SetupManagerButton.Content = setup.HasLocalConfiguration
            ? "Reparar Manager"
            : "Configurar Manager";
        SetupManagerButton.Visibility = setup.IsPackageComplete
            ? Visibility.Visible
            : Visibility.Collapsed;
        SetupManagerButton.IsEnabled = !setupInProgress;

        SetFeedback(message);
        UpdateSelection();
    }

    private async void ServicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (applyingInventory) return;
        logSequence = 0;
        logs.Clear();
        UpdateSelection();
        await RefreshLogsAsync(CancellationToken.None);
    }

    private void UpdateSelection()
    {
        var selected = SelectedService;
        var available = managerConnected && !operationInProgress;
        SelectedServiceName.Text = selected?.DisplayName ?? "Nenhum serviço selecionado";
        SelectedServicePath.Text = selected?.Executable ?? "Selecione um serviço para ver os detalhes.";
        if (selected is null) LogsTextBox.Text = "Selecione um serviço para carregar os logs.";

        var state = selected?.State;
        AddButton.IsEnabled = available;
        StartButton.IsEnabled = available && selected is not null && state is "stopped" or "missing";
        StopButton.IsEnabled = available && selected is not null && state is "running" or "startpending";
        RestartButton.IsEnabled = available && selected is not null && state == "running";
        EditButton.IsEnabled = available && selected is not null;
        RepairButton.IsEnabled = available && selected is not null && !selected.RegistrationMatches;
        RemoveButton.IsEnabled = available && selected is not null;
    }

    private async Task RefreshLogsAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedService;
        if (selected is null || !managerConnected) return;
        try
        {
            var entries = await manager.GetLogsAsync(
                selected.ServiceId,
                logSequence,
                200,
                cancellationToken);
            foreach (var entry in entries.OrderBy(item => item.Sequence))
            {
                if (entry.Sequence <= logSequence) continue;
                logs.Add(entry);
                logSequence = entry.Sequence;
            }

            if (logs.Count > 1_000) logs.RemoveRange(0, logs.Count - 1_000);
            LogsTextBox.Text = logs.Count == 0
                ? "Nenhuma saída registrada para este serviço."
                : string.Join(Environment.NewLine, logs.Select(FormatLog));
            LogsTextBox.ScrollToEnd();
        }
        catch (LocalManagerException)
        {
            LogsTextBox.Text = "Os logs deste serviço ainda não estão disponíveis.";
        }
        catch (InvalidDataException)
        {
            SetManagerUnavailable("O Manager local usa um protocolo incompatível com esta versão.");
        }
        catch (Exception exception) when (IsManagerUnavailable(exception))
        {
            SetManagerUnavailable("A conexão com o Manager local foi interrompida.");
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var choice = new AddChoiceWindow { Owner = this };
        if (choice.ShowDialog() != true) return;
        var editor = new ServiceEditorWindow(
            productRoot,
            definition: null,
            choice.BrowseExistingApplication) { Owner = this };
        if (editor.ShowDialog() != true || editor.Definition is null) return;
        await RunOperationAsync(async cancellationToken =>
        {
            await manager.CreateServiceAsync(editor.Definition, cancellationToken);
            await RefreshServicesAsync(cancellationToken);
            SetFeedback($"Serviço {editor.Definition.DisplayName} adicionado.");
        });
    }

    private async void SetupManagerButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (setupInProgress) return;
        var action = setup.HasLocalConfiguration ? "reparar" : "configurar";
        var answer = MessageBox.Show(
            $"Deseja {action} o Manager local?\n\n" +
            "O Windows solicitará permissão administrativa uma vez. " +
            "O Launcher continuará sendo executado sem elevação.",
            "Configuração do Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        setupInProgress = true;
        SetupManagerButton.IsEnabled = false;
        SetFeedback("Aguardando a confirmação administrativa do Windows...");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var outcome = await setup.InstallOrRepairAsync(timeout.Token);
            SetFeedback(outcome.Message);
            if (!outcome.Success) return;
            if (await WaitForManagerAsync(timeout.Token))
            {
                SetFeedback("Manager local configurado e conectado.");
            }
            else
            {
                SetManagerUnavailable(
                    "O Manager foi registrado, mas não ficou disponível no prazo esperado.");
            }
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            OperationCanceledException or
            System.ComponentModel.Win32Exception)
        {
            SetFeedback("Não foi possível concluir a configuração do Manager.");
        }
        finally
        {
            setupInProgress = false;
            SetupManagerButton.IsEnabled = true;
        }
    }

    private async Task<bool> WaitForManagerAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var inventory = await manager.GetServicesAsync(cancellationToken);
                managerConnected = true;
                SetManagerStatus(online: true);
                ApplyInventory(inventory);
                return true;
            }
            catch (Exception exception) when (exception is
                LocalManagerException or
                IOException or
                UnauthorizedAccessException or
                TimeoutException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        return false;
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selected = SelectedService;
        if (selected is null) return;
        await RunOperationAsync(async cancellationToken =>
        {
            var details = await manager.GetServiceAsync(selected.ServiceId, cancellationToken);
            var editor = new ServiceEditorWindow(
                productRoot,
                details.Definition,
                browseExistingApplication: false) { Owner = this };
            if (editor.ShowDialog() != true || editor.Definition is null) return;
            await manager.UpdateServiceAsync(editor.Definition, cancellationToken);
            await RefreshServicesAsync(cancellationToken);
            SetFeedback($"Serviço {editor.Definition.DisplayName} atualizado.");
        });
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("iniciado", manager.StartServiceAsync);

    private async void StopButton_Click(object sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("parado", manager.StopServiceAsync);

    private async void RestartButton_Click(object sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("reiniciado", manager.RestartServiceAsync);

    private async void RepairButton_Click(object sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("reparado", manager.RepairServiceAsync);

    private async Task RunServiceActionAsync(
        string result,
        Func<string, CancellationToken, Task> action)
    {
        var selected = SelectedService;
        if (selected is null) return;
        await RunOperationAsync(async cancellationToken =>
        {
            SetFeedback($"Executando ação em {selected.DisplayName}...");
            await action(selected.ServiceId, cancellationToken);
            await RefreshServicesAsync(cancellationToken);
            SetFeedback($"Serviço {result}.");
        });
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selected = SelectedService;
        if (selected is null) return;
        var answer = MessageBox.Show(
            $"Remover o serviço “{selected.DisplayName}”?\n\nO Manager irá parar o processo e excluir o registro gerenciado.",
            "Confirmar remoção",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        await RunOperationAsync(async cancellationToken =>
        {
            await manager.RemoveServiceAsync(selected.ServiceId, cancellationToken);
            ServicesList.SelectedItem = null;
            await RefreshServicesAsync(cancellationToken);
            SetFeedback("Serviço removido.");
        });
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        logs.Clear();
        LogsTextBox.Clear();
        SetFeedback("Visualização de logs limpa.");
    }

    private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        try
        {
            if (!string.IsNullOrWhiteSpace(LogsTextBox.Text)) Clipboard.SetText(LogsTextBox.Text);
            SetFeedback("Logs copiados.");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            SetFeedback("A área de transferência está ocupada. Tente novamente.");
        }
    }

    private void ExportLogsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selected = SelectedService;
        if (selected is null || logs.Count == 0)
        {
            SetFeedback("Não há logs para exportar.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"{selected.ServiceId}-logs.txt",
            DefaultExt = ".txt",
            Filter = "Texto UTF-8 (*.txt)|*.txt",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, LogsTextBox.Text, new UTF8Encoding(false));
            SetFeedback("Logs exportados.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetFeedback("Não foi possível exportar os logs para o local selecionado.");
        }
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        if (operationInProgress || !managerConnected) return;
        operationInProgress = true;
        UpdateSelection();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await operation(timeout.Token);
        }
        catch (LocalManagerException exception)
        {
            SetFeedback(exception.Message);
        }
        catch (InvalidDataException)
        {
            SetFeedback("O Manager retornou dados incompatíveis com esta versão.");
        }
        catch (Exception exception) when (IsManagerUnavailable(exception))
        {
            SetManagerUnavailable("O Manager local não respondeu. Verifique a instalação local.");
        }
        finally
        {
            operationInProgress = false;
            UpdateSelection();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        monitorTimer.Stop();
    }

    private void SetFeedback(string message) => FeedbackText.Text = message;

    private void SetManagerStatus(bool online)
    {
        ManagerStatusDot.Fill = (Brush)FindResource(online ? "SuccessBrush" : "MutedBrush");
        ManagerStatusText.Text = online ? "Online" : "Offline";
        ManagerStatusText.Foreground = (Brush)FindResource(online ? "SuccessBrush" : "MutedBrush");
    }

    private static string FormatLog(ServiceLogEntryV1 entry) =>
        $"[{entry.Timestamp.ToLocalTime():HH:mm:ss.fff}] [{entry.Stream.ToUpperInvariant()}] {entry.Message}";

    private static bool IsManagerUnavailable(Exception exception) => exception is
        IOException or
        TimeoutException or
        OperationCanceledException or
        UnauthorizedAccessException;
}
