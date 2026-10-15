using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Companion.Application.Voice;
using Microsoft.Win32.SafeHandles;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>A same-executable child born in an owned job, with only its three private pipe handles inherited.</summary>
internal sealed partial class VoiceWorkerProcess : IAsyncDisposable
{
    private readonly KernelHandle process;
    private readonly KernelHandle job;
    private readonly SafeFileHandle[] pipeHandles;
    private readonly object sync = new();
    private Task? stop;
    private Task? disposal;
    internal Stream Input { get; }
    internal Stream Output { get; }
    internal Stream Error { get; }
    internal int Id { get; }
    internal bool HasExited => WaitForSingleObject(process, 0) == 0;
    internal int ExitCode
    {
        get
        {
            if (!HasExited || !GetExitCodeProcess(process, out var code))
                throw new AudioInputException(AudioInputFailure.Unavailable);
            return unchecked((int)code);
        }
    }

    private VoiceWorkerProcess(KernelHandle process, KernelHandle job, int id,
        FileStream input, FileStream output, FileStream error, SafeFileHandle[] pipeHandles)
    {
        this.process = process; this.job = job; Id = id;
        Input = input; Output = output; Error = error;
        this.pipeHandles = pipeHandles;
    }

    internal static VoiceWorkerProcess Start(string executablePath, string workerArgument, uint memoryLimitMiB = 1024)
    {
        ValidateLaunch(executablePath, workerArgument, memoryLimitMiB);
        SafeFileHandle? inputRead = null, inputWrite = null, outputRead = null, outputWrite = null,
            errorRead = null, errorWrite = null;
        KernelHandle? child = null, childThread = null, ownedJob = null;
        FileStream? input = null, output = null, error = null;
        IntPtr attributes = IntPtr.Zero, handles = IntPtr.Zero, jobs = IntPtr.Zero, environment = IntPtr.Zero;
        var attributesInitialized = false;
        try
        {
            ownedJob = CreateBoundedJob(memoryLimitMiB);
            CreatePrivatePipe(out inputRead, out inputWrite, parentIsRead: false);
            CreatePrivatePipe(out outputRead, out outputWrite, parentIsRead: true);
            CreatePrivatePipe(out errorRead, out errorWrite, parentIsRead: true);
            nuint requiredBytes = 0;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref requiredBytes);
            if (requiredBytes == 0 || requiredBytes > 65536) throw NativeFailure();
            attributes = Marshal.AllocHGlobal(checked((int)requiredBytes));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref requiredBytes)) throw NativeFailure();
            attributesInitialized = true;
            handles = Marshal.AllocHGlobal(3 * IntPtr.Size);
            Marshal.WriteIntPtr(handles, 0, inputRead.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size, outputWrite.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, 2 * IntPtr.Size, errorWrite.DangerousGetHandle());
            jobs = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(jobs, ownedJob.DangerousGetHandle());
            // JOB_LIST assigns the job as part of CreateProcess, including failure cleanup.
            // This avoids leaving even a suspended child outside the kill-on-close job.
            if (!UpdateProcThreadAttribute(attributes, 0, 0x00020002, handles, (nuint)(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero) ||
                !UpdateProcThreadAttribute(attributes, 0, 0x0002000D, jobs, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw NativeFailure();
            var startup = new StartupInfoEx
            {
                StartupInfo = new()
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                    StandardInput = inputRead.DangerousGetHandle(),
                    StandardOutput = outputWrite.DangerousGetHandle(), StandardError = errorWrite.DangerousGetHandle()
                },
                AttributeList = attributes
            };
            environment = Marshal.StringToHGlobalUni(BuildEnvironmentBlock());
            var command = new StringBuilder('"' + executablePath + "\" " + workerArgument);
            const uint flags = 0x00000004 | 0x00000400 | 0x00080000 | 0x08000000;
            if (!CreateProcess(executablePath, command, IntPtr.Zero, IntPtr.Zero, true, flags,
                    environment, AppContext.BaseDirectory, ref startup, out var created)) throw NativeFailure();
            child = new KernelHandle(created.Process);
            childThread = new KernelHandle(created.Thread);
            // Never reopen a PID. All queries/termination use this original returned process handle.
            input = new FileStream(inputWrite, FileAccess.Write, 4096, isAsync: false);
            output = new FileStream(outputRead, FileAccess.Read, 4096, isAsync: false);
            error = new FileStream(errorRead, FileAccess.Read, 4096, isAsync: false);
            if (ResumeThread(childThread) == uint.MaxValue) throw NativeFailure();
            var result = new VoiceWorkerProcess(child, ownedJob, checked((int)created.ProcessId), input, output, error,
                [inputWrite, outputRead, errorRead]);
            child = null; ownedJob = null; input = null; output = null; error = null;
            inputWrite = null; outputRead = null; errorRead = null;
            return result;
        }
        catch (Exception errorValue) when (errorValue is Win32Exception or IOException or UnauthorizedAccessException)
        {
            throw new AudioInputException(AudioInputFailure.Unavailable);
        }
        finally
        {
            if (child is not null)
            {
                _ = TerminateProcess(child, 1);
                _ = WaitForSingleObject(child, 3000);
            }
            // A broken write pipe can throw while flushing on Dispose. Never let that
            // skip another pipe, kernel handle or the kill-on-close job.
            _ = Release(childThread); _ = Release(child); _ = Release(ownedJob);
            _ = Release(input); _ = Release(output); _ = Release(error);
            _ = Release(inputRead); _ = Release(inputWrite); _ = Release(outputRead); _ = Release(outputWrite);
            _ = Release(errorRead); _ = Release(errorWrite);
            if (attributesInitialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (jobs != IntPtr.Zero) Marshal.FreeHGlobal(jobs);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
        }
    }

    internal static void ValidateLaunch(string executablePath, string workerArgument, uint memoryLimitMiB)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess ||
            string.IsNullOrWhiteSpace(executablePath) || executablePath.Any(char.IsControl) ||
            !Path.IsPathFullyQualified(executablePath) || executablePath.StartsWith("\\\\", StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(executablePath), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) ||
            workerArgument is not ("--capture-worker" or "--recognition-worker" or "--voice-fixture-worker") ||
            memoryLimitMiB is < 64 or > 1024)
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
    }

    internal static string BuildEnvironmentBlock()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows, ["WINDIR"] = windows,
            ["PATH"] = Path.Combine(windows, "System32") + ";" + windows,
            ["TEMP"] = Path.GetTempPath(), ["TMP"] = Path.GetTempPath(),
            ["USERPROFILE"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["APPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ["LOCALAPPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["PROGRAMDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };
        return string.Join('\0', values.Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
    }

    internal Task WaitForExitAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var retained = false;
        try
        {
            process.DangerousAddRef(ref retained);
            using var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
            completed.SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
            if (WaitHandle.WaitAny([completed, cancellationToken.WaitHandle]) == 1)
                cancellationToken.ThrowIfCancellationRequested();
        }
        finally { if (retained) process.DangerousRelease(); }
    }, CancellationToken.None);

    internal Task StopAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > AudioInputLimits.CleanupDeadline)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        lock (sync) return stop ??= Task.Run(() =>
        {
            if (HasExited) return;
            if (!TerminateJobObject(job, 1) && !TerminateProcess(process, 1))
                throw new AudioInputException(AudioInputFailure.CleanupFailed);
            if (WaitForSingleObject(process, checked((uint)Math.Ceiling(timeout.TotalMilliseconds))) != 0)
                throw new AudioInputException(AudioInputFailure.CleanupFailed);
        });
    }

    public ValueTask DisposeAsync()
    {
        lock (sync) return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try { await StopAsync(AudioInputLimits.CleanupDeadline).ConfigureAwait(false); }
        finally
        {
            var released = Release(Input);
            released &= Release(Output);
            released &= Release(Error);
            foreach (var pipe in pipeHandles) released &= Release(pipe);
            released &= Release(process);
            released &= Release(job);
            if (!released) throw new AudioInputException(AudioInputFailure.CleanupFailed);
        }
    }

    private static bool Release(IDisposable? resource)
    {
        try { resource?.Dispose(); return true; }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return false; }
    }

    private static Win32Exception NativeFailure() => new(Marshal.GetLastWin32Error());
}
