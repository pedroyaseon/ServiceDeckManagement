using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Contracts.Manager;

namespace ServiceDeckManagement.Launcher;

public partial class MainWindow : Window, IAsyncDisposable
{
    private readonly ServiceDeckApiClient api;
    private readonly RealtimeService realtime;
    private readonly string productRoot;
    private readonly ObservableCollection<ManagedServiceSnapshotV1> services = [];
    private readonly List<ServiceLogEntryV1> logs = [];
    private readonly DispatcherTimer monitorTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool bootstrapRequired;
    private bool operationInProgress;
    private bool monitorInProgress;
    private long snapshotSequence;
    private long logSequence;

    public MainWindow(LauncherOptions options, string productRoot)
    {
        InitializeComponent();
        api = new ServiceDeckApiClient(options);
        realtime = new RealtimeService(options, () => api.AccessToken);
        this.productRoot = productRoot;
        ServicesGrid.ItemsSource = services;
        realtime.SnapshotReceived += Realtime_SnapshotReceived;
        realtime.ConnectionChanged += Realtime_ConnectionChanged;
        monitorTimer.Tick += MonitorTimer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private ManagedServiceSnapshotV1? SelectedService => ServicesGrid.SelectedItem as ManagedServiceSnapshotV1;

    private bool IsAdministrator => string.Equals(api.CurrentUser?.Role, ApiRolesV1.Administrator, StringComparison.Ordinal);

    private bool CanOperate => IsAdministrator || string.Equals(api.CurrentUser?.Role, ApiRolesV1.Operator, StringComparison.Ordinal);

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        SetFeedback("Conectando à API...");
        try
        {
            await UpdateHealthAsync();
            var status = await api.GetBootstrapStatusAsync(CancellationToken.None);
            bootstrapRequired = status.Required;
            BootstrapPanel.Visibility = bootstrapRequired ? Visibility.Visible : Visibility.Collapsed;
            AuthenticationHint.Text = bootstrapRequired
                ? "Informe o código exibido no console da API para criar o primeiro administrador."
                : "Use sua conta local da API.";
            LoginButton.Content = bootstrapRequired ? "Criar administrador" : "Entrar";
            SetFeedback(bootstrapRequired ? "Inicialização administrativa necessária." : "Aguardando autenticação.");
            UsernameTextBox.Focus();
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            SetStatus(ApiStatusDot, ApiStatusText, online: false);
            SetStatus(ManagerStatusDot, ManagerStatusText, online: false);
            AuthenticationError.Text = "A API local não está disponível. Inicie o componente API e abra novamente o Launcher.";
            SetFeedback("API indisponível.");
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (operationInProgress) return;
        AuthenticationError.Text = string.Empty;
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;
        if (username.Length == 0 || password.Length == 0)
        {
            AuthenticationError.Text = "Informe usuário e senha.";
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            if (bootstrapRequired)
            {
                await api.BootstrapAsync(BootstrapCodeTextBox.Text.Trim(), username, password, cancellationToken);
                bootstrapRequired = false;
            }

            var session = await api.LoginAsync(username, password, cancellationToken);
            AuthenticationOverlay.Visibility = Visibility.Collapsed;
            CurrentUserText.Text = $"{session.User.Username} · {RoleLabel(session.User.Role)}";
            AddButton.IsEnabled = IsAdministrator;
            await RefreshServicesAsync(cancellationToken);
            try
            {
                await realtime.StartAsync(cancellationToken);
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                SetFeedback("Conectado. Atualização em tempo real indisponível; usando sincronização periódica.");
            }
            monitorTimer.Start();
            SetFeedback("Ambiente conectado.");
        }, authenticationOperation: true);
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (monitorInProgress || operationInProgress || api.AccessToken is null) return;
        monitorInProgress = true;
        try
        {
            await UpdateHealthAsync();
            if (!realtime.IsConnected)
            {
                await RefreshServicesAsync(CancellationToken.None);
            }
            await RefreshLogsAsync(CancellationToken.None);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            await EndSessionAsync("Sua sessão expirou. Entre novamente.");
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            SetStatus(ApiStatusDot, ApiStatusText, online: false);
            SetStatus(ManagerStatusDot, ManagerStatusText, online: false);
            SetFeedback("Conexão com a API interrompida. Tentando novamente...");
        }
        finally
        {
            monitorInProgress = false;
        }
    }

    private async Task UpdateHealthAsync()
    {
        var health = await api.GetHealthAsync(CancellationToken.None);
        SetStatus(ApiStatusDot, ApiStatusText, string.Equals(health.Api, "online", StringComparison.Ordinal));
        SetStatus(ManagerStatusDot, ManagerStatusText, string.Equals(health.Manager, "online", StringComparison.Ordinal));
    }

    private async Task RefreshServicesAsync(CancellationToken cancellationToken)
    {
        var inventory = await api.GetServicesAsync(cancellationToken);
        ApplySnapshot(inventory);
    }

    private void ApplySnapshot(IReadOnlyList<ManagedServiceSnapshotV1> inventory)
    {
        var selectedId = SelectedService?.ServiceId;
        services.Clear();
        foreach (var service in inventory.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            services.Add(service);
        }
        ServiceCountText.Text = services.Count == 1 ? "1 serviço gerenciado" : $"{services.Count} serviços gerenciados";
        if (selectedId is not null)
        {
            ServicesGrid.SelectedItem = services.FirstOrDefault(item => string.Equals(item.ServiceId, selectedId, StringComparison.Ordinal));
        }
        UpdateSelection();
    }

    private void Realtime_SnapshotReceived(ServiceSnapshotEnvelopeV1 snapshot)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (snapshot.Sequence <= snapshotSequence) return;
            if (snapshotSequence != 0 && snapshot.Sequence > snapshotSequence + 1)
            {
                try
                {
                    await RefreshServicesAsync(CancellationToken.None);
                }
                catch (Exception exception) when (IsConnectionFailure(exception))
                {
                    SetFeedback("Falha ao recuperar o snapshot mais recente.");
                }
            }
            else
            {
                ApplySnapshot(snapshot.Services);
            }
            snapshotSequence = snapshot.Sequence;
        });
    }

    private void Realtime_ConnectionChanged(bool connected) => Dispatcher.InvokeAsync(() =>
    {
        SetFeedback(connected ? "Atualização em tempo real conectada." : "Tempo real desconectado; sincronização periódica ativa.");
    });

    private async void ServicesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        logSequence = 0;
        logs.Clear();
        UpdateSelection();
        await RefreshLogsAsync(CancellationToken.None);
    }

    private void UpdateSelection()
    {
        var selected = SelectedService;
        var available = !operationInProgress;
        SelectedServiceName.Text = selected?.DisplayName ?? "Nenhum serviço selecionado";
        SelectedServicePath.Text = selected?.Executable ?? "Selecione uma linha para ver o executável.";
        if (selected is null) LogsTextBox.Text = "Selecione um serviço para carregar os logs.";

        var state = selected?.State;
        AddButton.IsEnabled = available && IsAdministrator;
        StartButton.IsEnabled = available && CanOperate && selected is not null && state is "stopped" or "missing";
        StopButton.IsEnabled = available && CanOperate && selected is not null && state is "running" or "startpending";
        RestartButton.IsEnabled = available && CanOperate && selected is not null && state == "running";
        EditButton.IsEnabled = available && IsAdministrator && selected is not null;
        RepairButton.IsEnabled = available && IsAdministrator && selected is not null && !selected.RegistrationMatches;
        RemoveButton.IsEnabled = available && IsAdministrator && selected is not null;
    }

    private async Task RefreshLogsAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedService;
        if (selected is null || api.AccessToken is null) return;
        try
        {
            var entries = await api.GetLogsAsync(selected.ServiceId, logSequence, 200, cancellationToken);
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
        catch (ApiException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            LogsTextBox.Text = "Os logs deste serviço ainda não estão disponíveis.";
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var choice = new AddChoiceWindow { Owner = this };
        if (choice.ShowDialog() != true) return;
        var editor = new ServiceEditorWindow(productRoot, definition: null, choice.BrowseExistingApplication) { Owner = this };
        if (editor.ShowDialog() != true || editor.Definition is null) return;
        await RunOperationAsync(async cancellationToken =>
        {
            await api.CreateServiceAsync(editor.Definition, cancellationToken);
            await RefreshServicesAsync(cancellationToken);
            SetFeedback($"Serviço {editor.Definition.DisplayName} adicionado.");
        });
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selected = SelectedService;
        if (selected is null) return;
        await RunOperationAsync(async cancellationToken =>
        {
            var details = await api.GetServiceAsync(selected.ServiceId, cancellationToken);
            var editor = new ServiceEditorWindow(productRoot, details.Definition, browseExistingApplication: false) { Owner = this };
            if (editor.ShowDialog() != true || editor.Definition is null) return;
            await api.UpdateServiceAsync(editor.Definition, cancellationToken);
            await RefreshServicesAsync(cancellationToken);
            SetFeedback($"Serviço {editor.Definition.DisplayName} atualizado.");
        });
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e) => await RunServiceActionAsync("iniciado", api.StartServiceAsync);

    private async void StopButton_Click(object sender, RoutedEventArgs e) => await RunServiceActionAsync("parado", api.StopServiceAsync);

    private async void RestartButton_Click(object sender, RoutedEventArgs e) => await RunServiceActionAsync("reiniciado", api.RestartServiceAsync);

    private async void RepairButton_Click(object sender, RoutedEventArgs e) => await RunServiceActionAsync("reparado", api.RepairServiceAsync);

    private async Task RunServiceActionAsync(string result, Func<string, CancellationToken, Task> action)
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
            await api.RemoveServiceAsync(selected.ServiceId, cancellationToken);
            ServicesGrid.SelectedItem = null;
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

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation, bool authenticationOperation = false)
    {
        if (operationInProgress) return;
        operationInProgress = true;
        LoginButton.IsEnabled = false;
        UpdateSelection();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await operation(timeout.Token);
        }
        catch (ApiException exception)
        {
            if (authenticationOperation) AuthenticationError.Text = exception.Message;
            SetFeedback(exception.Message);
        }
        catch (Exception exception) when (IsConnectionFailure(exception))
        {
            const string message = "A API não respondeu. Verifique se o componente está em execução.";
            if (authenticationOperation) AuthenticationError.Text = message;
            SetFeedback(message);
        }
        catch (InvalidDataException)
        {
            SetFeedback("A API retornou dados incompatíveis com esta versão do Launcher.");
        }
        finally
        {
            operationInProgress = false;
            LoginButton.IsEnabled = true;
            UpdateSelection();
        }
    }

    private async Task EndSessionAsync(string message)
    {
        monitorTimer.Stop();
        await realtime.DisposeAsync();
        AuthenticationOverlay.Visibility = Visibility.Visible;
        AuthenticationError.Text = message;
        CurrentUserText.Text = "Não autenticado";
        services.Clear();
        AddButton.IsEnabled = false;
        UpdateSelection();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        monitorTimer.Stop();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await api.LogoutAsync(timeout.Token);
        }
        catch (Exception exception) when (IsConnectionFailure(exception) || exception is ApiException)
        {
            _ = exception;
        }
        await realtime.DisposeAsync();
        api.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SetFeedback(string message) => FeedbackText.Text = message;

    private void SetStatus(System.Windows.Shapes.Ellipse dot, TextBlock label, bool online)
    {
        dot.Fill = (Brush)FindResource(online ? "SuccessBrush" : "MutedBrush");
        label.Text = online ? "Online" : "Offline";
        label.Foreground = (Brush)FindResource(online ? "SuccessBrush" : "MutedBrush");
    }

    private static string FormatLog(ServiceLogEntryV1 entry) =>
        $"[{entry.Timestamp.ToLocalTime():HH:mm:ss.fff}] [{entry.Stream.ToUpperInvariant()}] {entry.Message}";

    private static string RoleLabel(string role) => role switch
    {
        ApiRolesV1.Administrator => "Administrador",
        ApiRolesV1.Operator => "Operador",
        _ => "Visualizador"
    };

    private static bool IsConnectionFailure(Exception exception) => exception is
        HttpRequestException or
        TaskCanceledException or
        TimeoutException or
        IOException;
}
