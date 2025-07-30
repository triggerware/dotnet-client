using System.Text.Json;
using TWClients.JsonRpcMessages;

namespace TWClients;

public class DataBlob
{
	/// <summary>
	///     creates a DataBlob from the bytes of an input stream
	/// </summary>
	/// <param name="stream">the stream containing the bytes of the data blob</param>
	/// <param name="mimeType">
	///     the mime type of the data
	///     <param>
	///         <exception cref="IOException">if the stream cannot be read</exception>
	public DataBlob(Stream stream, string mimeType)
    {
        using (var memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            Data = memoryStream.ToArray();
        }

        MimeType = mimeType;
        Access = null;
    }

	/// <summary>
	///     creates a DataBlob from an array of bytes
	/// </summary>
	/// <param name="data">the data bytes of the blob</param>
	/// <param name="mimeType">the mime type of the data</param>
	public DataBlob(byte[] data, string mimeType)
    {
        Data = data;
        MimeType = mimeType;
        Access = null;
    }

    public byte[] Data { get; private set; }
    public string MimeType { get; }
    public DeferredBlobAccess Access { get; }

    public byte[] GetData()
    {
        if (Data == null)
            try
            {
                var brs = Access.Brs;
                if (brs == null) return null;
                Data = brs.DownloadBlobContent(Access.ConnectorKey);
            }
            catch (JsonRpcException e)
            {
                // TODO log something
                e = e;
            }

        return Data;
    }

    public class DeferredBlobAccess
    {
        private DeferredBlobAccess(string connectorId, JsonElement connectorKey)
        {
            Brs = null;
            ConnectorId = connectorId;
            ConnectorKey = connectorKey;
        }

        private DeferredBlobAccess(IBlobReferenceSupportErased brs, JsonElement connectorKey)
        {
            Brs = brs;
            ConnectorId = brs.ConnectorId;
            ConnectorKey = connectorKey;
        }

        public IBlobReferenceSupportErased Brs { get; }
        public JsonElement ConnectorKey { get; }
        public string ConnectorId { get; }
    }
}