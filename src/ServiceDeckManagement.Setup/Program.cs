namespace ServiceDeckManagement.Setup;

internal static class Program
{
    [STAThread]
    public static Task<int> Main(string[] args) => SetupProgram.RunAsync(args);
}
