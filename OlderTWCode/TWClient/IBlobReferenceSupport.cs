using System.Text.Json;
using TWClients.JsonRpcMessages;

namespace TWClients;


public interface IBlobReferenceSupportErased
{
    public byte[] DownloadBlobContent(JsonElement connectorKey);
    public string ConnectorId { get; }
}

public interface IBlobReferenceSupport<E> : IBlobReferenceSupportErased where E : JsonRpcException
{
    public byte[] DownloadBlobContent(JsonElement connectorKey);
    public string ConnectorId { get; }
}