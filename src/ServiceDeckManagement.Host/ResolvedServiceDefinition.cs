using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Host;

/// <summary>
/// Definição validada e os caminhos canônicos usados durante a execução.
/// </summary>
public sealed record ResolvedServiceDefinition(
    ServiceDefinitionV1 Definition,
    string ExecutablePath,
    string WorkingDirectoryPath);
