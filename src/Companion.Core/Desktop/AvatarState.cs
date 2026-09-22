namespace Companion.Core.Desktop;

public enum AvatarState { Idle, Listening, Thinking, Working, Speaking, Notifying, Sleeping, Error }

/// <summary>Activity ownership prevents a late completion from clearing newer work.</summary>
public sealed class AvatarStateMachine
{
    private readonly object gate = new();
    private readonly Dictionary<long, AvatarState> activities = [];
    private long nextId;
    private bool sleeping;
    public event EventHandler? Changed;

    public AvatarState Current
    {
        get
        {
            lock (gate)
                return sleeping ? AvatarState.Sleeping : activities.Values.DefaultIfEmpty(AvatarState.Idle)
                    .MaxBy(Priority);
        }
    }

    public IDisposable Begin(AvatarState state)
    {
        if (!Enum.IsDefined(state) || state is AvatarState.Idle or AvatarState.Sleeping)
            throw new ArgumentOutOfRangeException(nameof(state));
        long id;
        lock (gate)
        {
            if (sleeping) throw new InvalidOperationException("Wake the companion before starting an activity.");
            id = checked(++nextId);
            activities.Add(id, state);
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return new Activity(this, id);
    }

    public void SetSleeping(bool value)
    {
        lock (gate)
        {
            if (sleeping == value) return;
            sleeping = value;
            // Sleep ends visual ownership. It does not cancel a future provider's work;
            // the application must cancel that work separately before entering sleep.
            activities.Clear();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void End(long id)
    {
        lock (gate) { if (!activities.Remove(id)) return; }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static int Priority(AvatarState state) => state switch
    {
        AvatarState.Error => 7, AvatarState.Listening => 6, AvatarState.Speaking => 5,
        AvatarState.Working => 4, AvatarState.Thinking => 3, AvatarState.Notifying => 2, _ => 0
    };

    private sealed class Activity(AvatarStateMachine owner, long id) : IDisposable
    {
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) owner.End(id); }
    }
}
