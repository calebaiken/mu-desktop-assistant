using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

/// <summary>One shared speech operation, routing the user's selected local voice without replacement.</summary>
public sealed class CompositeSpeechOutput : ISpeechOutput
{
    private readonly ISpeechOutput windows;
    private readonly ISpeechOutput? neural;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? active;
    private Task? disposal;
    private bool disposed;
    private SpeechFailure? neuralListingFailure;
    private SpeechFailure? windowsListingFailure;

    public CompositeSpeechOutput(ISpeechOutput windows, ISpeechOutput? neural)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.neural = neural;
    }

    /// <summary>Optional provider's last content-free listing failure; Windows voices may remain usable.</summary>
    public SpeechFailure? NeuralListingFailure { get { lock (sync) return neuralListingFailure; } }
    public SpeechFailure? WindowsListingFailure { get { lock (sync) return windowsListingFailure; } }

    public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async token =>
        {
            IReadOnlyList<SpeechVoice> neuralVoices = [];
            IReadOnlyList<SpeechVoice> windowsVoices = [];
            SpeechException? neuralError = null;
            SpeechException? windowsError = null;
            if (neural is not null)
            {
                try { neuralVoices = await neural.GetVoicesAsync(token).ConfigureAwait(false); }
                catch (SpeechException error) { neuralError = error; }
            }
            try { windowsVoices = await windows.GetVoicesAsync(token).ConfigureAwait(false); }
            catch (SpeechException error) { windowsError = error; }
            token.ThrowIfCancellationRequested();
            lock (sync)
            {
                neuralListingFailure = neural is null ? SpeechFailure.Unavailable : neuralError?.Failure;
                windowsListingFailure = windowsError?.Failure;
            }
            if (windowsError is not null && neuralVoices.Count == 0) throw windowsError;
            if (neuralError is not null && windowsVoices.Count == 0) throw neuralError;
            var result = neuralVoices.Concat(windowsVoices).ToArray();
            if (result.Length > SpeechLimits.MaximumVoices + 4 || result.Select(voice => voice.Id).Distinct(StringComparer.Ordinal).Count() != result.Length)
                throw SpeechProtocol.Invalid();
            foreach (var voice in result) SpeechProtocol.ValidateVoice(voice);
            if (neuralVoices.Any(voice => !KokoroNeuralSpeechOutput.IsNeuralVoice(voice.Id)) ||
                windowsVoices.Any(voice => voice.Id.StartsWith("kokoro:", StringComparison.Ordinal))) throw SpeechProtocol.Invalid();
            return (IReadOnlyList<SpeechVoice>)Array.AsReadOnly(result);
        }, cancellationToken);

    public Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress,
        CancellationToken cancellationToken = default)
    {
        SpeechLimits.Validate(request);
        var selected = request.VoiceId.StartsWith("kokoro:", StringComparison.Ordinal);
        if (selected && !KokoroNeuralSpeechOutput.IsNeuralVoice(request.VoiceId)) throw new SpeechException(SpeechFailure.InvalidRequest);
        return RunAsync(async token =>
        {
            var output = selected ? neural ?? throw new SpeechException(SpeechFailure.Unavailable) : windows;
            await output.SpeakAsync(request, progress, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource completion;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (active is not null) throw new SpeechException(SpeechFailure.Busy);
            active = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try { return await operation(cancellation.Token).ConfigureAwait(false); }
        finally
        {
            lock (sync) active = null;
            completion.TrySetResult();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposal is not null) return new(disposal);
            disposed = true;
            return new(disposal = DisposeCoreAsync(active?.Task));
        }
    }

    private async Task DisposeCoreAsync(Task? operation)
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        // Each call is wrapped so a synchronous provider failure cannot skip the other provider.
        static async Task CloseAsync(ISpeechOutput output) => await output.DisposeAsync().ConfigureAwait(false);
        var disposals = new List<Task> { CloseAsync(windows) };
        if (neural is not null && !ReferenceEquals(neural, windows)) disposals.Add(CloseAsync(neural));
        if (operation is not null) disposals.Add(operation);
        try { await Task.WhenAll(disposals).WaitAsync(SpeechLimits.CleanupDeadline).ConfigureAwait(false); }
        catch (TimeoutException) { throw new SpeechException(SpeechFailure.CleanupFailed); }
    }
}
