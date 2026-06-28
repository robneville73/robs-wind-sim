using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RobsWindSim.Forms;
using RobsWindSim.Models;
using RobsWindSim.Native;
using RobsWindSim.Services;

namespace RobsWindSim;

public enum TrayConnectionState
{
    Disconnected,
    ConnectedIdle,
    ConnectedActive
}

public sealed class AppTrayContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly ManualTestSpeedSource _manualSource = new();
    private readonly IracingSpeedSource _iracingSource = new();
    private readonly SerialFanController _serial = new();
    private readonly GlobalHotkeys _hotkeys;
    private readonly Form _hostForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _loopTimer;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _masterToggleItem;

    private SettingsForm? _settingsForm;
    private FanOutput _lastOutput = FanOutput.Zero;
    private TrayConnectionState _trayState = TrayConnectionState.Disconnected;

    public AppTrayContext(AppSettings settings)
    {
        _settings = settings;
        _manualSource.SetTestSpeed(_settings.TestSpeedMph);

        _hostForm = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Opacity = 0,
            Size = new Size(0, 0),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000)
        };
        MainForm = _hostForm;
        _hostForm.Load += (_, _) => _hostForm.Hide();

        _hotkeys = new GlobalHotkeys();
        _hotkeys.MasterTogglePressed += OnMasterToggleHotkey;
        _hotkeys.IdleUpPressed += OnIdleUpHotkey;
        _hotkeys.IdleDownPressed += OnIdleDownHotkey;
        _hotkeys.ApplyBindings(_settings);

        _masterToggleItem = new ToolStripMenuItem(
            "Master On/Off",
            null,
            (_, _) => ToggleMaster());
        _masterToggleItem.Checked = _settings.MasterEnabled;

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Settings", null, (_, _) => ShowSettings());
        _trayMenu.Items.Add(_masterToggleItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "Robs Wind Sim",
            ContextMenuStrip = _trayMenu,
            Icon = TrayIconFactory.Create(TrayConnectionState.Disconnected),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _serial.ConfigurePort(_settings.ComPort);

        _loopTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _loopTimer.Tick += (_, _) => OnLoopTick();
        _loopTimer.Start();

        UpdateTrayPresentation();
    }

    public AppSettings Settings => _settings;
    public ManualTestSpeedSource ManualSource => _manualSource;
    public IracingSpeedSource IracingSource => _iracingSource;
    public SerialFanController Serial => _serial;
    public FanOutput LastOutput => _lastOutput;

    public void ShowSettings()
    {
        void Show()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(this);
                _settingsForm.FormClosed += (_, e) =>
                {
                    if (e.CloseReason == CloseReason.UserClosing)
                        _settingsForm = null;
                };
            }

            _settingsForm.RefreshFromSettings();
            _settingsForm.Show(_hostForm);
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
        }

        if (_hostForm.InvokeRequired)
            _hostForm.BeginInvoke(Show);
        else
            Show();
    }

    public void SaveSettings()
    {
        _settings.TestSpeedMph = _manualSource.CurrentSpeedMph;
        _settings.Save();
        _serial.ConfigurePort(_settings.ComPort);
        _serial.RequestImmediateSend();
        _hotkeys.ApplyBindings(_settings);
        UpdateTrayPresentation();
    }

    public void StartTestSweep()
    {
        _manualSource.StartSweep(_settings.MaxSpeedMph);
        _settings.TestModeEnabled = true;
        SaveSettings();
    }

    public void ToggleMaster()
    {
        _settings.MasterEnabled = !_settings.MasterEnabled;
        _masterToggleItem.Checked = _settings.MasterEnabled;
        SaveSettings();
    }

    private void OnMasterToggleHotkey() => BeginInvokeOnUi(ToggleMaster);
    private void OnIdleUpHotkey() => BeginInvokeOnUi(AdjustIdleUp);
    private void OnIdleDownHotkey() => BeginInvokeOnUi(AdjustIdleDown);

    private void AdjustIdleUp()
    {
        _settings.IdleSpeedPercent = Math.Min(100, _settings.IdleSpeedPercent + _settings.IdleStepPercent);
        SaveSettings();
    }

    private void AdjustIdleDown()
    {
        _settings.IdleSpeedPercent = Math.Max(0, _settings.IdleSpeedPercent - _settings.IdleStepPercent);
        SaveSettings();
    }

    private void OnLoopTick()
    {
        _manualSource.Tick();

        var activeSource = _settings.TestModeEnabled ? (ISpeedSource)_manualSource : _iracingSource;
        _lastOutput = FanOutputPipeline.Compute(_settings, activeSource);
        _serial.Tick(_lastOutput);

        UpdateTrayPresentation();
        _settingsForm?.UpdateLiveValues();
    }

    private void UpdateTrayPresentation()
    {
        var newState = ResolveTrayState();
        if (newState != _trayState)
        {
            _trayState = newState;
            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = TrayIconFactory.Create(newState);
            TrayIconFactory.DisposeIcon(oldIcon);
        }

        var tooltip = $"Robs Wind Sim L:{_lastOutput.LeftPercent:F0}% R:{_lastOutput.RightPercent:F0}%";
        if (tooltip.Length > 63)
            tooltip = tooltip[..63];
        _notifyIcon.Text = tooltip;
        _masterToggleItem.Checked = _settings.MasterEnabled;
        _masterToggleItem.Text = _settings.MasterEnabled ? "Master On (enabled)" : "Master Off (disabled)";
    }

    private TrayConnectionState ResolveTrayState()
    {
        if (!_serial.IsConnected)
            return TrayConnectionState.Disconnected;

        if (_lastOutput.IsActive)
            return TrayConnectionState.ConnectedActive;

        return TrayConnectionState.ConnectedIdle;
    }

    private void BeginInvokeOnUi(Action action)
    {
        if (_hostForm.IsDisposed)
            return;

        if (_hostForm.InvokeRequired)
            _hostForm.BeginInvoke(action);
        else
            action();
    }

    private void ExitApplication()
    {
        _loopTimer.Stop();
        _notifyIcon.Visible = false;
        TrayIconFactory.DisposeIcon(_notifyIcon.Icon);
        _notifyIcon.Dispose();
        _hotkeys.Dispose();
        _serial.Dispose();
        _hostForm.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loopTimer.Dispose();
            TrayIconFactory.DisposeIcon(_notifyIcon.Icon);
            _notifyIcon.Dispose();
            _hotkeys.Dispose();
            _serial.Dispose();
            _hostForm.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal static class TrayIconFactory
{
    public static Icon Create(TrayConnectionState state)
    {
        var color = state switch
        {
            TrayConnectionState.ConnectedActive => Color.LimeGreen,
            TrayConnectionState.ConnectedIdle => Color.Gold,
            _ => Color.IndianRed
        };

        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 4, 4, 24, 24);

        var handle = bitmap.GetHicon();
        try
        {
            using var extracted = Icon.FromHandle(handle);
            return (Icon)extracted.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static void DisposeIcon(Icon? icon)
    {
        if (icon != null)
            icon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);
}
