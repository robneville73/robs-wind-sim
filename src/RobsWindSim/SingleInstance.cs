namespace RobsWindSim;

internal static class SingleInstance
{
    public const string MutexName = @"Local\robs-wind-sim-single-instance";
    public const string ActivateEventName = @"Local\robs-wind-sim-activate";

    public static bool TryAcquire(out Mutex? mutex)
    {
        mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
            return true;

        mutex.Dispose();
        mutex = null;
        SignalActivate();
        return false;
    }

    public static void SignalActivate()
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // First instance not running yet.
        }
    }

    public static IDisposable ListenForActivate(Action onActivate)
    {
        return new ActivateListener(onActivate);
    }

    private sealed class ActivateListener : IDisposable
    {
        private readonly EventWaitHandle _activateEvent;
        private readonly Thread _thread;
        private volatile bool _disposed;

        public ActivateListener(Action onActivate)
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _thread = new Thread(() =>
            {
                while (!_disposed)
                {
                    if (!_activateEvent.WaitOne(500))
                        continue;

                    onActivate();
                }
            })
            {
                IsBackground = true,
                Name = "RobsWindSim-ActivateListener"
            };
            _thread.Start();
        }

        public void Dispose()
        {
            _disposed = true;
            _activateEvent.Set();
            _thread.Join(TimeSpan.FromSeconds(1));
            _activateEvent.Dispose();
        }
    }
}
