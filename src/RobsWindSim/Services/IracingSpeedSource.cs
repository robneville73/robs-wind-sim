using System.Diagnostics;
using IRSDKSharper;
using RobsWindSim.Models;

namespace RobsWindSim.Services;

public sealed class IracingSpeedSource : ISpeedSource, IDisposable
{
    private const double MetersPerSecondToMph = 2.23694;

    private readonly AppSettings _settings;
    private readonly object _lock = new();
    private readonly IRacingSdk _sdk = new();

    private double _currentSpeedMph;
    private bool _isLiveSession;

    public IracingSpeedSource(AppSettings settings)
    {
        _settings = settings;
        _sdk.UpdateInterval = 2;
        _sdk.OnException += OnException;
        _sdk.OnSessionInfo += OnSessionInfo;
        _sdk.OnTelemetryData += OnTelemetryData;
    }

    public string Name => "iRacing";

    public bool IsConnected
    {
        get
        {
            lock (_lock)
                return _sdk.IsConnected;
        }
    }

    public bool IsLiveSession
    {
        get
        {
            lock (_lock)
                return _isLiveSession;
        }
    }

    public double CurrentSpeedMph
    {
        get
        {
            lock (_lock)
            {
                if (!_sdk.IsConnected)
                    return 0;

                if (!_isLiveSession && !_settings.ReplayModeEnabled)
                    return 0;

                return _currentSpeedMph;
            }
        }
    }

    public void Start()
    {
        if (!_sdk.IsStarted)
            _sdk.Start();
    }

    public void Dispose()
    {
        if (_sdk.IsStarted)
            _sdk.Stop();
    }

    private void OnSessionInfo()
    {
        lock (_lock)
            _isLiveSession = _sdk.Data.SessionInfo.WeekendInfo.SimMode == "full";
    }

    private void OnTelemetryData()
    {
        var speedMps = _sdk.Data.GetFloat("Speed");
        var speedMph = Math.Max(0, speedMps * MetersPerSecondToMph);

        lock (_lock)
            _currentSpeedMph = speedMph;
    }

    private void OnException(Exception exception)
    {
        Debug.WriteLine($"iRacing SDK exception: {exception}");
        _sdk.Stop();
        _sdk.Start();
    }
}
