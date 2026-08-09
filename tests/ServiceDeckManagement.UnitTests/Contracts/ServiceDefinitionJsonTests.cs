using System.Text.Json;
using System.Text.Json.Nodes;
using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.UnitTests.Contracts;

public sealed class ServiceDefinitionJsonTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesContract()
    {
        var definition = TestServiceDefinitions.Valid();

        var json = ServiceDefinitionJson.Serialize(definition);
        var restored = ServiceDefinitionJson.Deserialize(json);

        Assert.Equal(definition.Id, restored.Id);
        Assert.Equal(definition.Arguments, restored.Arguments);
        Assert.Equal(definition.Logging, restored.Logging);
    }

    [Fact]
    public void Deserialize_RejectsUnknownProperty()
    {
        var json = ServiceDefinitionJson.Serialize(
            TestServiceDefinitions.Valid());
        json = json.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unexpected\": true,",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
            ServiceDefinitionJson.Deserialize(json));
    }

    [Fact]
    public void Deserialize_RejectsMissingRequiredProperty()
    {
        var json = ServiceDefinitionJson.Serialize(
            TestServiceDefinitions.Valid());
        var document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.Remove("arguments"));

        Assert.Throws<JsonException>(() =>
            ServiceDefinitionJson.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void Deserialize_RejectsDuplicateProperty()
    {
        var json = ServiceDefinitionJson.Serialize(
            TestServiceDefinitions.Valid());
        json = json.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
            ServiceDefinitionJson.Deserialize(json));
    }

    [Fact]
    public void PublicExample_DeserializesStrictly()
    {
        var root = FindRepositoryRoot();
        var json = File.ReadAllText(Path.Combine(
            root,
            "config",
            "examples",
            "service-definition.example.json"));

        var definition = ServiceDefinitionJson.Deserialize(json);

        Assert.Equal("example-api", definition.Id);
        Assert.Equal(1, definition.SchemaVersion);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".servicedeck-root")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
