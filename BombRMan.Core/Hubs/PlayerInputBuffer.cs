namespace BombRMan.Hubs;

internal sealed class PlayerInputBuffer : IDisposable
{
    private readonly object _lock = new();
    private KeyboardState _latest;
    private bool _hasLatest;
    private int _lastAcceptedId = -1;
    private long _lastReceivedTimestamp;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _hasLatest ? 1 : 0;
            }
        }
    }

    public bool TryAccept(KeyboardState input, long receivedTimestamp)
    {
        lock (_lock)
        {
            if (input.Id <= _lastAcceptedId)
            {
                return false;
            }

            if (_hasLatest)
            {
                _latest.Dispose();
            }

            _latest = input;
            _hasLatest = true;
            _lastAcceptedId = input.Id;
            _lastReceivedTimestamp = receivedTimestamp;
            return true;
        }
    }

    public bool TryTakeLatest(out KeyboardState input)
    {
        lock (_lock)
        {
            if (!_hasLatest)
            {
                input = default;
                return false;
            }

            input = _latest;
            _latest = default;
            _hasLatest = false;
            return true;
        }
    }

    public bool IsExpired(long now, long timeoutTicks)
    {
        lock (_lock)
        {
            return _lastAcceptedId >= 0 && now - _lastReceivedTimestamp >= timeoutTicks;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_hasLatest)
            {
                _latest.Dispose();
                _latest = default;
                _hasLatest = false;
            }
        }
    }
}
