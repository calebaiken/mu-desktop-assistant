using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Companion.Infrastructure.Lifetime;

public sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    private readonly FileStream instanceLock;
    private readonly string pipeName;
    private readonly CancellationTokenSource stopping = new();
    private Task? listener;

    private SingleInstanceCoordinator(FileStream instanceLock, string pipeName)
    {
        this.instanceLock = instanceLock;
        this.pipeName = pipeName;
    }

    public static SingleInstanceCoordinator? TryAcquire(string directory)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        Directory.CreateDirectory(fullPath);
        var session = Process.GetCurrentProcess().SessionId;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())));
        try
        {
            var handle = new FileStream(Path.Combine(fullPath, $"instance-{session}.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new(handle, $"DesktopCompanion-{session}-{hash}");
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 32 or 33)
        {
            return null;
        }
    }

    public void StartListening(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        if (listener is not null) throw new InvalidOperationException("Activation listener already started.");
        listener = ListenAsync(activate);
    }

    // Exposed so the composition root observes background faults instead of losing them.
    public Task Completion => listener ?? Task.CompletedTask;

    private async Task ListenAsync(Action activate)
    {
        while (!stopping.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await server.WaitForConnectionAsync(stopping.Token).ConfigureAwait(false);
                using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                requestDeadline.CancelAfter(TimeSpan.FromSeconds(2));
                var request = new byte[1];
                var length = await server.ReadAsync(request, requestDeadline.Token).ConfigureAwait(false);
                if (length == 1 && request[0] == 1)
                {
                    activate();
                    await server.WriteAsync(new byte[] { 1 }, requestDeadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { /* An idle client is disconnected after its bounded deadline. */ }
            catch (IOException) { /* A disconnected client cannot terminate the activation listener. */ }
        }
    }

    public static async Task<bool> ActivateExistingAsync(string directory, CancellationToken cancellationToken)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())));
        var pipe = $"DesktopCompanion-{Process.GetCurrentProcess().SessionId}-{hash}";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(deadline.Token).ConfigureAwait(false);
            await client.WriteAsync(new byte[] { 1 }, deadline.Token).ConfigureAwait(false);
            var reply = new byte[1];
            return await client.ReadAsync(reply, deadline.Token).ConfigureAwait(false) == 1 && reply[0] == 1;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        catch (IOException) { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        try { if (listener is not null) await listener.ConfigureAwait(false); }
        finally
        {
            instanceLock.Dispose();
            stopping.Dispose();
        }
    }
}
