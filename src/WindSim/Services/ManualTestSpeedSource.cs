namespace WindSim.Services;

public sealed class ManualTestSpeedSource : ISpeedSource
{
    private readonly object _lock = new();
    private double _testSpeedMph;
    private bool _isSweeping;
    private DateTime _sweepStartUtc;
    private double _sweepMaxMph;
    private TimeSpan _sweepDuration = TimeSpan.FromSeconds(25);

    public string Name => "Manual Test";
    public bool IsConnected => true;

    public double CurrentSpeedMph
    {
        get
        {
            lock (_lock)
            {
                if (_isSweeping)
                    return ComputeSweepSpeed();

                return _testSpeedMph;
            }
        }
    }

    public bool IsSweeping
    {
        get
        {
            lock (_lock)
                return _isSweeping;
        }
    }

    public void SetTestSpeed(double mph)
    {
        lock (_lock)
        {
            _isSweeping = false;
            _testSpeedMph = Math.Max(0, mph);
        }
    }

    public void StartSweep(double maxMph, TimeSpan? duration = null)
    {
        lock (_lock)
        {
            _sweepMaxMph = Math.Max(0, maxMph);
            _sweepDuration = duration ?? TimeSpan.FromSeconds(25);
            _sweepStartUtc = DateTime.UtcNow;
            _isSweeping = true;
        }
    }

    public void StopSweep()
    {
        lock (_lock)
            _isSweeping = false;
    }

    public void Tick()
    {
        lock (_lock)
        {
            if (!_isSweeping)
                return;

            var speed = ComputeSweepSpeed();
            _testSpeedMph = speed;

            var elapsed = DateTime.UtcNow - _sweepStartUtc;
            if (elapsed >= _sweepDuration * 2)
                _isSweeping = false;
        }
    }

    private double ComputeSweepSpeed()
    {
        var elapsed = DateTime.UtcNow - _sweepStartUtc;
        var halfDuration = _sweepDuration.TotalSeconds;

        if (elapsed.TotalSeconds <= halfDuration)
            return _sweepMaxMph * (elapsed.TotalSeconds / halfDuration);

        if (elapsed.TotalSeconds <= halfDuration * 2)
        {
            var descendElapsed = elapsed.TotalSeconds - halfDuration;
            return _sweepMaxMph * (1 - descendElapsed / halfDuration);
        }

        _isSweeping = false;
        return 0;
    }
}
