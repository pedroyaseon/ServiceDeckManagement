namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

public interface ITransportKeyProvider
{
    Task<byte[]> GetKeyAsync(CancellationToken cancellationToken);
}
