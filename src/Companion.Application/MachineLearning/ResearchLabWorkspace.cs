namespace Companion.Application.MachineLearning;

/// <summary>Borrowed, project-bound service. Only the workspace owns its shutdown.</summary>
public sealed record ResearchLabSelection(ResearchProject Project, IMlLabService Service);

/// <summary>One App-owned Lab. Admission and project switching share the same lock.</summary>
public sealed class ResearchLabWorkspace(IResearchProjectStore projects, Func<ResearchProject, IMlLabService> factory) : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private Lease? current;
    private Task? opening, disposal, cleanup;
    private int active;
    private bool switching, disposed, poisoned;
    public bool IsBusy { get { lock (sync) return switching || active != 0; } }
    // Reopening the active project's view is allowed while its jobs run. This
    // grants no project switch and exposes no new service/factory admission.
    public ResearchLabSelection? Current
    {
        get { lock (sync) return !disposed && !poisoned && !switching && current is { } selected ? new(selected.Project, selected) : null; }
    }

    public Task<ResearchLabSelection> OpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            RequireAvailable();
            if (switching || active != 0) throw Busy();
            switching = true;
            var task = OpenCoreAsync(id, cancellationToken);
            opening = task;
            return task;
        }
    }

    private async Task<ResearchLabSelection> OpenCoreAsync(Guid id, CancellationToken caller)
    {
        await Task.Yield(); // Register the transition before it can finish.
        using var token = CancellationTokenSource.CreateLinkedTokenSource(caller, lifetime.Token);
        try
        {
            var project = await projects.OpenAsync(id, token.Token).ConfigureAwait(false);
            Lease? previous;
            lock (sync)
            {
                RequireAvailable(); token.Token.ThrowIfCancellationRequested();
                if (current is { } selected && selected.Project == project) return new(project, selected);
                previous = current;
                current = null; // Old views/planners permanently lose admission before cleanup.
            }
            if (previous is not null)
            {
                try
                {
                    var stopping = Task.Run(async () => await previous.Inner.DisposeAsync().ConfigureAwait(false));
                    lock (sync) cleanup = stopping;
                    // After retirement starts, finish confirming cleanup even if the picker closes.
                    await stopping.WaitAsync(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
                }
                catch (Exception) { lock (sync) poisoned = true; throw Unavailable(); }
            }
            lock (sync)
            {
                RequireAvailable(); token.Token.ThrowIfCancellationRequested();
                var service = factory(project);
                if (service.ProjectDirectory != project.Directory)
                {
                    poisoned = true;
                    cleanup = Task.Run(async () => await service.DisposeAsync().ConfigureAwait(false));
                    throw Unavailable();
                }
                current = new(this, project, service);
                return new(project, current);
            }
        }
        finally { lock (sync) switching = false; }
    }

    private Task<MlResult> Execute(Lease lease, MlAction action, MlLimits? limits, CancellationToken token)
    {
        lock (sync)
        {
            RequireAvailable();
            if (switching || !ReferenceEquals(current, lease)) throw new MlException("stale_project", "This Lab view is no longer active. Open the selected project again.");
            token.ThrowIfCancellationRequested();
            active++; // Before calling the service: Jobs can still be empty at admission.
            try { return ObserveAsync(lease.Inner.ExecuteAsync(action, limits, token)); }
            catch { active--; throw; }
        }
    }

    private async Task<MlResult> ObserveAsync(Task<MlResult> work)
    {
        try { return await work.ConfigureAwait(false); }
        finally { lock (sync) active--; }
    }

    private void RequireAvailable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (poisoned) throw Unavailable();
    }
    private static MlException Busy() => new("project_busy", "Wait for Lab work to finish or cancel its jobs before changing projects.");
    private static MlException Unavailable() => new("cleanup", "Lab cleanup was not confirmed. Restart Mu before opening another project.");

    public ValueTask DisposeAsync()
    {
        lock (sync) return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Task? transition;
        lock (sync) { disposed = true; lifetime.Cancel(); transition = opening; }
        if (transition is not null) { try { await transition.ConfigureAwait(false); } catch (Exception) { } }
        Lease? selected; Task? retained;
        lock (sync) { selected = current; current = null; retained = cleanup; }
        try
        {
            if (retained is not null) await retained.ConfigureAwait(false);
            if (selected is not null) await selected.Inner.DisposeAsync().ConfigureAwait(false);
        }
        finally { lifetime.Dispose(); }
        lock (sync) if (poisoned) throw Unavailable();
    }

    private sealed class Lease(ResearchLabWorkspace owner, ResearchProject project, IMlLabService inner) : IMlLabService
    {
        public ResearchProject Project { get; } = project;
        public IMlLabService Inner { get; } = inner;
        public string ProjectDirectory => Project.Directory;
        public IReadOnlyList<MlJobUpdate> Jobs => Inner.Jobs;
        public event Action<MlJobUpdate>? JobChanged { add => Inner.JobChanged += value; remove => Inner.JobChanged -= value; }
        public void Cancel(Guid jobId) => Inner.Cancel(jobId);
        public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
            => owner.Execute(this, action, limits, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.FromException(new MlException("project_ownership", "The App-owned workspace controls Lab shutdown; closing a view does not dispose its service."));
    }
}
