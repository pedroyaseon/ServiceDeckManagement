using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ServiceDeckManagement.Launcher;

public sealed class ServiceStateLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        "running" => "Em execução",
        "stopped" => "Parado",
        "startpending" => "Iniciando",
        "stoppending" => "Parando",
        "missing" => "Não registrado",
        "paused" => "Pausado",
        _ => "Indisponível"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ServiceStateBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Running = Frozen("#173B35");
    private static readonly SolidColorBrush Pending = Frozen("#3B321A");
    private static readonly SolidColorBrush Stopped = Frozen("#222C38");
    private static readonly SolidColorBrush Missing = Frozen("#3A2025");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        "running" => Running,
        "startpending" or "stoppending" => Pending,
        "missing" => Missing,
        _ => Stopped
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class StartModeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        "automatic" => "Automático",
        "delayed" => "Automático (atrasado)",
        "manual" => "Manual",
        "disabled" => "Desativado",
        _ => "Não informado"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
