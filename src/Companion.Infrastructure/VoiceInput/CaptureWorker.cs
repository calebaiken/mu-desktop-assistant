using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Private one-operation worker. Device listing never activates an audio client.</summary>
public static class CaptureWorker
{
    public static async Task<int> RunAsync()
    {
        if (!Console.IsInputRedirected || !Console.IsOutputRedirected || !HasPrivatePipes()) return 2;
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        using var lifetime = new CancellationTokenSource(AudioInputLimits.InitializationDeadline +
            AudioInputLimits.CaptureDuration + AudioInputLimits.CleanupDeadline);
        CaptureCommand? command = null;
        var sequence = 0;
        try
        {
            var read = CaptureProtocol.ReadCommandAsync(input, lifetime.Token);
            ObserveFault(read);
            command = await read.WaitAsync(AudioInputLimits.CommandDeadline, lifetime.Token).ConfigureAwait(false);
            if (command.Kind == "devices")
            {
                var devicesTask = OnStaAsync(() => WasapiCapture.Enumerate(lifetime.Token));
                ObserveFault(devicesTask);
                var devices = await devicesTask.WaitAsync(AudioInputLimits.InitializationDeadline, lifetime.Token).ConfigureAwait(false);
                var payload = CaptureProtocol.SerializeDevices(devices);
                try { await SendAsync(new(CaptureEventKind.Devices, command.OperationId, 0, payload)).ConfigureAwait(false); }
                finally { CryptographicOperations.ZeroMemory(payload); }
                return 0;
            }

            using var stop = new ManualResetEvent(false);
            var protocolFailure = 0;
            async Task ObserveControlAsync()
            {
                try
                {
                    var control = new byte[1];
                    var receivedStop = false;
                    while (true)
                    {
                        var count = await input.ReadAsync(control, lifetime.Token).ConfigureAwait(false);
                        if (count == 0)
                        {
                            if (!receivedStop) await lifetime.CancelAsync().ConfigureAwait(false);
                            return;
                        }
                        if (receivedStop || control[0] != CaptureProtocol.StopCommand)
                        {
                            Interlocked.Exchange(ref protocolFailure, 1);
                            await lifetime.CancelAsync().ConfigureAwait(false);
                            return;
                        }
                        receivedStop = true;
                        stop.Set();
                    }
                }
                catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException)
                { await lifetime.CancelAsync().ConfigureAwait(false); }
            }
            var controlTask = ObserveControlAsync();
            ObserveFault(controlTask);
            var queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(AudioInputLimits.QueueFrames)
            {
                FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = OnStaAsync(() =>
            {
                using var packetizer = new CapturePacketizer(frame => queue.Writer.TryWrite(frame));
                try { return WasapiCapture.Capture(command.Request!, packetizer, () => started.TrySetResult(), stop, lifetime.Token); }
                finally { queue.Writer.TryComplete(); }
            });
            ObserveFault(operation);
            try
            {
                await Task.WhenAny(started.Task, operation).WaitAsync(AudioInputLimits.InitializationDeadline, lifetime.Token).ConfigureAwait(false);
                if (started.Task.IsCompletedSuccessfully)
                    await SendAsync(new(CaptureEventKind.Started, command.OperationId, 0, [])).ConfigureAwait(false);
                await foreach (var frame in queue.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
                {
                    try { await SendAsync(new(CaptureEventKind.Pcm, command.OperationId, ++sequence, frame)).ConfigureAwait(false); }
                    finally { CryptographicOperations.ZeroMemory(frame); }
                }
                var limited = await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
                if (Volatile.Read(ref protocolFailure) != 0) throw CaptureProtocol.Invalid();
                await SendAsync(new(CaptureEventKind.Stopped, command.OperationId, sequence, [(byte)(limited ? 1 : 0)])).ConfigureAwait(false);
                return 0;
            }
            finally
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                queue.Writer.TryComplete();
                while (queue.Reader.TryRead(out var frame)) CryptographicOperations.ZeroMemory(frame);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
        {
            if (command is not null)
            {
                var failure = error switch
                {
                    AudioInputException capture => capture.Failure,
                    TimeoutException or OperationCanceledException => AudioInputFailure.Timeout,
                    _ => AudioInputFailure.Unavailable
                };
                try
                {
                    using var failureDeadline = new CancellationTokenSource(AudioInputLimits.StopDeadline);
                    var write = CaptureProtocol.WriteEventAsync(output,
                        new(CaptureEventKind.Failed, command.OperationId, sequence, CaptureProtocol.FailurePayload(failure)), failureDeadline.Token);
                    ObserveFault(write);
                    await write.WaitAsync(failureDeadline.Token).ConfigureAwait(false);
                }
                catch (Exception sendError) when (sendError is IOException or OperationCanceledException or AudioInputException or ObjectDisposedException) { }
            }
            return 2;
        }
        finally { await lifetime.CancelAsync().ConfigureAwait(false); }

        async Task SendAsync(CaptureEvent message)
        {
            var write = CaptureProtocol.WriteEventAsync(output, message, lifetime.Token);
            ObserveFault(write);
            await write.WaitAsync(lifetime.Token).ConfigureAwait(false);
        }
    }

    private static Task<T> OnStaAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
            { completion.TrySetException(error); }
        }) { IsBackground = true, Name = "Local audio input operation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void ObserveFault(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static bool HasPrivatePipes() => new[] { -10, -11, -12 }.All(id => GetFileType(GetStdHandle(id)) == 3);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);
}
