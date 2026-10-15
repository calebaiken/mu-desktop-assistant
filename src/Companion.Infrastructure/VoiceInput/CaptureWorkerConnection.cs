namespace Companion.Infrastructure.VoiceInput;

/// <summary>Internal fakeable supervision seam; production uses only the fixed owned capture worker.</summary>
internal interface ICaptureWorkerConnection : IAsyncDisposable
{
    Stream Input { get; }
    Stream Output { get; }
    Stream Error { get; }
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken token);
    Task StopAsync(TimeSpan timeout);
}

internal sealed class CaptureWorkerConnection(VoiceWorkerProcess process) : ICaptureWorkerConnection
{
    public Stream Input => process.Input;
    public Stream Output => process.Output;
    public Stream Error => process.Error;
    public int Id => process.Id;
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;
    public Task WaitForExitAsync(CancellationToken token) => process.WaitForExitAsync(token);
    public Task StopAsync(TimeSpan timeout) => process.StopAsync(timeout);
    public ValueTask DisposeAsync() => process.DisposeAsync();
}
