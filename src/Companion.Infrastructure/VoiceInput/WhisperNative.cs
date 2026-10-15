using System.Runtime.InteropServices;
using System.Text;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Native bindings exist only in the one-operation recognition child, never in the desktop.</summary>
internal sealed class WhisperNative
{
    private const DllImportSearchPath Search = DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32;
    // Static roots survive native context/backend destruction and process shutdown. Never decode log text.
    private static int nativeError;
    private static readonly LogCallback DiscardLog = (level, _, _) =>
    {
        // GGML_LOG_LEVEL_ERROR is 4 in this pinned ABI. Never dereference or retain the text.
        if (level == 4) Interlocked.Exchange(ref nativeError, 1);
    };
    private readonly nint whisper;
    private readonly nint ggml;
    private readonly nint ggmlBase;
    private readonly PointerFunction contextDefaults;
    private readonly FullDefaultsFunction fullDefaults;
    private readonly FreeFunction freeParameters, freeContextParameters, freeContext, freeVad, freeSegments;
    private readonly ContextInitFunction initContext;
    private readonly VadInitFunction initVad;
    private readonly VadSegmentsFunction vadSegments;
    private readonly CountFunction countVadSegments, countTextSegments;
    private readonly SegmentTimeFunction vadStart, vadEnd;
    private readonly FullFunction full;
    private readonly SegmentTextFunction segmentText;

    internal WhisperNative(string runtimeDirectory)
    {
        if (!WhisperAbi.IsSupported) throw Unavailable();
        Interlocked.Exchange(ref nativeError, 0); // Exactly one operation in this process.
        // The launcher supplies a cleared environment; refuse a direct worker with an out-of-tree backend override.
        if (Environment.GetEnvironmentVariable("GGML_BACKEND_PATH") is not null) throw Unavailable();
        var assembly = typeof(WhisperNative).Assembly;
        ggmlBase = NativeLibrary.Load(Path.Combine(runtimeDirectory, "ggml-base.dll"), assembly, Search);
        ggml = NativeLibrary.Load(Path.Combine(runtimeDirectory, "ggml.dll"), assembly, Search);
        whisper = NativeLibrary.Load(Path.Combine(runtimeDirectory, "whisper.dll"), assembly, Search);
        // These module handles intentionally remain resident until this child exits. Contexts are freed
        // explicitly; unloading a module while backend registries retain callbacks would be unsafe.
        Function<SetLogFunction>("whisper_log_set")(DiscardLog, 0);
        if (ReadUtf8(Function<PointerFunction>("whisper_version")(), 32) != "1.9.4") throw Unavailable();
        Function<LoadBackendsFunction>(ggml, "ggml_backend_load_all_from_path")(runtimeDirectory);
        ThrowNativeError();
        contextDefaults = Function<PointerFunction>("whisper_context_default_params_by_ref");
        fullDefaults = Function<FullDefaultsFunction>("whisper_full_default_params_by_ref");
        freeParameters = Function<FreeFunction>("whisper_free_params");
        freeContextParameters = Function<FreeFunction>("whisper_free_context_params");
        freeContext = Function<FreeFunction>("whisper_free");
        freeVad = Function<FreeFunction>("whisper_vad_free");
        freeSegments = Function<FreeFunction>("whisper_vad_free_segments");
        initContext = Function<ContextInitFunction>("whisper_init_with_params");
        initVad = Function<VadInitFunction>("whisper_vad_init_with_params");
        vadSegments = Function<VadSegmentsFunction>("whisper_vad_segments_from_samples");
        countVadSegments = Function<CountFunction>("whisper_vad_segments_n_segments");
        vadStart = Function<SegmentTimeFunction>("whisper_vad_segments_get_segment_t0");
        vadEnd = Function<SegmentTimeFunction>("whisper_vad_segments_get_segment_t1");
        full = Function<FullFunction>("whisper_full");
        countTextSegments = Function<CountFunction>("whisper_full_n_segments");
        segmentText = Function<SegmentTextFunction>("whisper_full_get_segment_text");
    }

    internal SpeechRecognitionResult Transcribe(RecognitionAssets assets, float[] samples, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (start, length) = DetectSpeech(assets.VadModel, samples, cancellationToken);
        if (length == 0) return new SpeechRecognitionResult("", false);
        var selected = new float[length];
        samples.AsSpan(start, length).CopyTo(selected);
        nint context = 0;
        nint language = 0;
        try
        {
            var parametersPointer = contextDefaults();
            if (parametersPointer == 0) throw Unavailable();
            WhisperContextParameters contextParameters;
            try { contextParameters = Marshal.PtrToStructure<WhisperContextParameters>(parametersPointer); }
            finally { freeContextParameters(parametersPointer); }
            contextParameters.UseGpu = 0;
            contextParameters.FlashAttention = 0;
            contextParameters.DtwTokenTimestamps = 0;
            using (var reader = new WhisperModelReader(assets.BaseModel, cancellationToken))
            {
                var loader = reader.Loader;
                context = initContext(ref loader, contextParameters);
                cancellationToken.ThrowIfCancellationRequested();
                if (context == 0 || reader.Failed) throw Unavailable();
                ThrowNativeError();
            }

            var fullPointer = fullDefaults(0); // Greedy sampling.
            if (fullPointer == 0) throw Unavailable();
            WhisperFullParameters parameters;
            try { parameters = Marshal.PtrToStructure<WhisperFullParameters>(fullPointer); }
            finally { freeParameters(fullPointer); }
            parameters.Threads = 2;
            parameters.Translate = 0;
            parameters.NoContext = 1;
            parameters.NoTimestamps = 1;
            parameters.SingleSegment = 0;
            parameters.PrintSpecial = parameters.PrintProgress = parameters.PrintRealtime = parameters.PrintTimestamps = 0;
            parameters.TokenTimestamps = parameters.DebugMode = parameters.Tinydiarize = 0;
            parameters.InitialPrompt = parameters.PromptTokens = parameters.SuppressRegex = 0;
            parameters.PromptTokenCount = 0;
            parameters.CarryInitialPrompt = parameters.DetectLanguage = 0;
            parameters.SuppressBlank = parameters.SuppressNonSpeechTokens = 1;
            parameters.Temperature = 0;
            parameters.TemperatureIncrement = 0;
            parameters.GreedyBestOf = 1;
            parameters.Vad = 0; // The separately inspected VAD span has already been selected.
            parameters.VadModelPath = 0;
            language = Marshal.StringToCoTaskMemUTF8("en");
            parameters.Language = language;
            AbortCallback abort = _ => cancellationToken.IsCancellationRequested ? (byte)1 : (byte)0;
            parameters.AbortCallback = Marshal.GetFunctionPointerForDelegate(abort);
            parameters.AbortUserData = 0;
            var status = full(context, parameters, selected, selected.Length);
            GC.KeepAlive(abort);
            cancellationToken.ThrowIfCancellationRequested();
            if (status != 0) throw Unavailable();
            ThrowNativeError();
            var count = countTextSegments(context);
            if (count is < 0 or > 256) throw Invalid();
            var text = new StringBuilder();
            for (var index = 0; index < count; index++)
            {
                var segment = ReadUtf8(segmentText(context, index), 8_000);
                if (text.Length + segment.Length > AudioInputLimits.MaximumTextCharacters)
                    throw new AudioInputException(AudioInputFailure.Overflow);
                text.Append(segment);
            }
            var resultText = text.ToString().Trim();
            var result = new SpeechRecognitionResult(resultText, resultText.Length > 0);
            AudioInputLimits.Validate(result);
            return result;
        }
        finally
        {
            Array.Clear(selected);
            if (context != 0) freeContext(context);
            if (language != 0) Marshal.FreeCoTaskMem(language);
            GC.KeepAlive(this);
            GC.KeepAlive(ggmlBase);
        }
    }

    private (int Start, int Length) DetectSpeech(FileStream model, float[] samples, CancellationToken cancellationToken)
    {
        nint context = 0;
        nint segments = 0;
        try
        {
            using (var reader = new WhisperModelReader(model, cancellationToken))
            {
                var loader = reader.Loader;
                context = initVad(ref loader, new WhisperVadContextParameters { Threads = 2, UseGpu = 0, GpuDevice = 0 });
                cancellationToken.ThrowIfCancellationRequested();
                if (context == 0 || reader.Failed) throw Unavailable();
                ThrowNativeError();
            }
            var parameters = new WhisperVadParameters
            {
                Threshold = 0.5f, MinimumSpeechMilliseconds = 250, MinimumSilenceMilliseconds = 100,
                MaximumSpeechSeconds = 30, PaddingMilliseconds = 150, SampleOverlap = 0.1f
            };
            segments = vadSegments(context, parameters, samples, samples.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (segments == 0) throw Unavailable();
            // This native version can log a failed VAD compute and still return partial
            // probabilities. A non-null segment object alone is not proof of success.
            ThrowNativeError();
            var count = countVadSegments(segments);
            if (count is < 0 or > 128) throw Invalid();
            if (count == 0) return (0, 0);
            var bounds = new (float Start, float End)[count];
            for (var index = 0; index < count; index++) bounds[index] = (vadStart(segments, index), vadEnd(segments, index));
            return RecognitionAudio.SelectSpeechSpan(bounds, samples.Length);
        }
        finally
        {
            if (segments != 0) freeSegments(segments);
            if (context != 0) freeVad(context);
        }
    }

    internal static string ReadUtf8(nint pointer, int maximumBytes)
    {
        if (pointer == 0) throw Invalid();
        var bytes = new byte[maximumBytes];
        try
        {
            var length = 0;
            while (length < maximumBytes)
            {
                var item = Marshal.ReadByte(pointer, length);
                if (item == 0) return new UTF8Encoding(false, true).GetString(bytes, 0, length);
                bytes[length++] = item;
            }
            throw new AudioInputException(AudioInputFailure.Overflow);
        }
        catch (DecoderFallbackException) { throw Invalid(); }
        finally { Array.Clear(bytes); }
    }

    private T Function<T>(string name) where T : Delegate => Function<T>(whisper, name);
    private static T Function<T>(nint module, string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
    private static AudioInputException Invalid() => new(AudioInputFailure.ProtocolError);
    private static AudioInputException Unavailable() => new(AudioInputFailure.Unavailable);
    private static void ThrowNativeError() { if (Volatile.Read(ref nativeError) != 0) throw Unavailable(); }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint PointerFunction();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint FullDefaultsFunction(int strategy);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeFunction(nint pointer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint ContextInitFunction(ref WhisperModelLoader loader, WhisperContextParameters parameters);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint VadInitFunction(ref WhisperModelLoader loader, WhisperVadContextParameters parameters);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint VadSegmentsFunction(nint context, WhisperVadParameters parameters, [In] float[] samples, int sampleCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CountFunction(nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate float SegmentTimeFunction(nint segments, int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FullFunction(nint context, WhisperFullParameters parameters, [In] float[] samples, int sampleCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SegmentTextFunction(nint context, int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetLogFunction(LogCallback callback, nint data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LogCallback(int level, nint text, nint data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte AbortCallback(nint data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LoadBackendsFunction([MarshalAs(UnmanagedType.LPUTF8Str)] string directory);
}

internal sealed class WhisperModelReader : IDisposable
{
    private readonly FileStream stream;
    private readonly CancellationToken cancellationToken;
    private readonly byte[] buffer = new byte[65_536];
    private readonly ReadCallback read;
    private readonly EofCallback eof;
    private readonly CloseCallback close;
    internal bool Failed { get; private set; }
    internal WhisperModelLoader Loader => new()
    {
        Context = 0, Read = Marshal.GetFunctionPointerForDelegate(read),
        Eof = Marshal.GetFunctionPointerForDelegate(eof), Close = Marshal.GetFunctionPointerForDelegate(close)
    };

    internal WhisperModelReader(FileStream stream, CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.cancellationToken = cancellationToken;
        stream.Position = 0;
        read = Read;
        eof = Eof;
        close = _ => { }; // RecognitionAssets owns the held file handle.
    }

    private nuint Read(nint _, nint output, nuint requested)
    {
        try
        {
            if (requested > (nuint)stream.Length || output == 0) { Failed = true; return 0; }
            var remaining = checked((int)requested);
            var total = 0;
            while (remaining > 0)
            {
                if (cancellationToken.IsCancellationRequested) { Failed = true; return 0; }
                var count = stream.Read(buffer, 0, Math.Min(remaining, buffer.Length));
                if (count == 0) break;
                Marshal.Copy(buffer, 0, checked(output + total), count);
                total += count;
                remaining -= count;
            }
            return (nuint)total;
        }
        catch (Exception) { Failed = true; return 0; } // No managed exception may cross a native callback.
    }

    private byte Eof(nint _)
    {
        try { return cancellationToken.IsCancellationRequested || stream.Position >= stream.Length ? (byte)1 : (byte)0; }
        catch (Exception) { Failed = true; return 1; }
    }

    public void Dispose()
    {
        Array.Clear(buffer);
        GC.KeepAlive(read); GC.KeepAlive(eof); GC.KeepAlive(close);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint ReadCallback(nint context, nint output, nuint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte EofCallback(nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CloseCallback(nint context);
}
