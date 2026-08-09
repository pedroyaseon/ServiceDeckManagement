namespace ServiceDeckManagement.Manager;

internal static class Program
{
    public static Task<int> Main(string[] args) => ManagerProgram.RunAsync(args);
}
