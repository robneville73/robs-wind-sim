using System.Runtime.InteropServices;
using RobsWindSim.Models;

namespace RobsWindSim.Native;

public sealed class GlobalHotkeys : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly Dictionary<int, Action> _handlers = new();
    private bool _disposed;

    public event Action? MasterTogglePressed;
    public event Action? IdleUpPressed;
    public event Action? IdleDownPressed;

    public GlobalHotkeys()
    {
        CreateHandle(new CreateParams());
    }

    public void ApplyBindings(AppSettings settings)
    {
        UnregisterAll();
        Register(1, settings.MasterToggleHotkey, () => MasterTogglePressed?.Invoke());
        Register(2, settings.IdleUpHotkey, () => IdleUpPressed?.Invoke());
        Register(3, settings.IdleDownHotkey, () => IdleDownPressed?.Invoke());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && _handlers.TryGetValue(m.WParam.ToInt32(), out var handler))
            handler();

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnregisterAll();
        DestroyHandle();
    }

    private void Register(int id, HotkeyBinding binding, Action handler)
    {
        if (binding.Key == Keys.None)
            return;

        if (RegisterHotKey(Handle, id, binding.Modifiers, (uint)binding.Key))
            _handlers[id] = handler;
    }

    private void UnregisterAll()
    {
        foreach (var id in _handlers.Keys.ToList())
            UnregisterHotKey(Handle, id);

        _handlers.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
