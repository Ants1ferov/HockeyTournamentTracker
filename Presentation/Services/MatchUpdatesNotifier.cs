using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.Services;

public readonly record struct MatchUpdatedMessage(
    Guid TournamentId,
    Guid MatchId,
    Guid? StageId,
    MatchStatus Status);

public interface IMatchUpdatesListener
{
    void OnMatchUpdated(MatchUpdatedMessage message);
}

public interface IMatchUpdatesNotifier
{
    void Subscribe(IMatchUpdatesListener listener);
    void Unsubscribe(IMatchUpdatesListener listener);
    void Publish(MatchUpdatedMessage message);
}

public sealed class MatchUpdatesNotifier : IMatchUpdatesNotifier
{
    private readonly object _sync = new();
    private readonly List<WeakReference<IMatchUpdatesListener>> _listeners = new();

    public void Subscribe(IMatchUpdatesListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_sync)
        {
            CleanupDeadListenersLocked();
            if (_listeners.Any(wr => wr.TryGetTarget(out var existing) && ReferenceEquals(existing, listener)))
                return;

            _listeners.Add(new WeakReference<IMatchUpdatesListener>(listener));
        }
    }

    public void Unsubscribe(IMatchUpdatesListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_sync)
        {
            _listeners.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, listener));
        }
    }

    public void Publish(MatchUpdatedMessage message)
    {
        List<IMatchUpdatesListener> listeners;
        lock (_sync)
        {
            CleanupDeadListenersLocked();
            listeners = _listeners
                .Select(wr => wr.TryGetTarget(out var target) ? target : null)
                .Where(l => l is not null)
                .Cast<IMatchUpdatesListener>()
                .ToList();
        }

        foreach (var listener in listeners)
            listener.OnMatchUpdated(message);
    }

    private void CleanupDeadListenersLocked()
    {
        _listeners.RemoveAll(wr => !wr.TryGetTarget(out _));
    }
}
