namespace Temporalio.Tests.Extensions.Gcp.CloudRun.WorkerId;

using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Minimal in-process HTTP server that stands in for the Google Cloud Run metadata server. It
/// records the raw request headers it receives (so tests can assert the
/// <c>Metadata-Flavor: Google</c> header) and returns a configurable status code and body.
/// </summary>
internal sealed class CloudRunMetadataServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly int statusCode;
    private readonly string reasonPhrase;
    private readonly string body;
    private readonly List<string> requests = new();
    private readonly object gate = new();

    public CloudRunMetadataServer(int statusCode = 200, string reasonPhrase = "OK", string body = "")
    {
        this.statusCode = statusCode;
        this.reasonPhrase = reasonPhrase;
        this.body = body;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Uri = new Uri($"http://127.0.0.1:{port}/computeMetadata/v1/instance/id");
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>
    /// Gets the metadata instance-id URI clients should be pointed at.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// Gets the raw header block of every request the server has received.
    /// </summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (gate)
            {
                return requests.ToList();
            }
        }
    }

    public void Dispose() => listener.Dispose();

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception ex)
                when (ex is ObjectDisposedException or SocketException or InvalidOperationException)
            {
                // Listener was stopped/disposed; end the loop.
                return;
            }

            try
            {
                await HandleConnectionAsync(client);
            }
            catch (Exception ex)
                when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // Ignore per-connection socket errors during tests.
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var request = await ReadRequestHeadersAsync(stream);
        lock (gate)
        {
            requests.Add(request);
        }

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var responseHeader =
            $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
            "Content-Type: text/plain\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(responseHeader);
        await stream.WriteAsync(headerBytes.AsMemory());
        await stream.WriteAsync(bodyBytes.AsMemory());
        await stream.FlushAsync();
    }

    private static async Task<string> ReadRequestHeadersAsync(NetworkStream stream)
    {
        var buffer = new byte[1024];
        var builder = new StringBuilder();
        while (!builder.ToString().Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer.AsMemory());
            if (read <= 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return builder.ToString();
    }
}
