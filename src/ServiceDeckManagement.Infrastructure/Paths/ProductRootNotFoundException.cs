namespace ServiceDeckManagement.Infrastructure.Paths;

public sealed class ProductRootNotFoundException(string message)
    : InvalidOperationException(message);
