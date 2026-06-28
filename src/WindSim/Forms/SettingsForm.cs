using WindSim.Models;
using WindSim.Services;

namespace WindSim.Forms;

public sealed class SettingsForm : Form
{
    private readonly AppTrayContext _app;

    private ComboBox _comPortCombo = null!;
    private Label _arduinoStatusLabel = null!;
    private Label _iracingStatusLabel = null!;
    private Label _masterStatusLabel = null!;
    private Label _liveOutputLabel = null!;
    private NumericUpDown _maxSpeedInput = null!;
    private ComboBox _curveTypeCombo = null!;
    private NumericUpDown _curveExponentInput = null!;
    private CheckBox _leftEnabledCheck = null!;
    private CheckBox _rightEnabledCheck = null!;
    private TrackBar _leftMaxPowerSlider = null!;
    private TrackBar _rightMaxPowerSlider = null!;
    private Label _leftMaxPowerLabel = null!;
    private Label _rightMaxPowerLabel = null!;
    private CheckBox _syncChannelsCheck = null!;
    private TrackBar _idleSpeedSlider = null!;
    private Label _idleSpeedLabel = null!;
    private NumericUpDown _idleStepInput = null!;
    private CheckBox _testModeCheck = null!;
    private TrackBar _testSpeedSlider = null!;
    private Label _testSpeedLabel = null!;
    private Button _runSweepButton = null!;
    private Button _masterHotkeyButton = null!;
    private Button _idleUpHotkeyButton = null!;
    private Button _idleDownHotkeyButton = null!;

    private ToolTip _toolTip = null!;
    private GroupBox _channelsGroup = null!;
    private Panel _leftMaxPowerPanel = null!;
    private Panel _rightMaxPowerPanel = null!;
    private Label _leftMaxPowerCaption = null!;

    private HotkeyBinding? _capturingBinding;
    private Button? _capturingButton;
    private bool _isLoading;

    public SettingsForm(AppTrayContext app)
    {
        _app = app;
        InitializeComponent();
        RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        _isLoading = true;
        try
        {
            var settings = _app.Settings;

            PopulateComPorts(settings.ComPort);
            _maxSpeedInput.Value = Math.Clamp((decimal)settings.MaxSpeedMph, _maxSpeedInput.Minimum, _maxSpeedInput.Maximum);
            _curveTypeCombo.SelectedItem = settings.CurveType.ToString();
            _curveExponentInput.Value = Math.Clamp((decimal)settings.CurveExponent, _curveExponentInput.Minimum, _curveExponentInput.Maximum);
            _leftEnabledCheck.Checked = settings.LeftChannelEnabled;
            _rightEnabledCheck.Checked = settings.RightChannelEnabled;
            _leftMaxPowerSlider.Value = (int)Math.Clamp(settings.LeftMaxPowerPercent, 0, 100);
            _rightMaxPowerSlider.Value = (int)Math.Clamp(settings.RightMaxPowerPercent, 0, 100);
            _syncChannelsCheck.Checked = settings.SyncChannels;
            _idleSpeedSlider.Value = (int)Math.Clamp(settings.IdleSpeedPercent, 0, 100);
            _idleStepInput.Value = Math.Clamp((decimal)settings.IdleStepPercent, _idleStepInput.Minimum, _idleStepInput.Maximum);
            _testModeCheck.Checked = settings.TestModeEnabled;
            _testSpeedSlider.Maximum = (int)Math.Max(1, settings.MaxSpeedMph);
            _testSpeedSlider.Value = (int)Math.Clamp(_app.ManualSource.CurrentSpeedMph, 0, _testSpeedSlider.Maximum);

            UpdateChannelUiState();
            UpdateCurveUiState();
            UpdateLabels();
            UpdateStatusHeader();
            UpdateHotkeyButtons();
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void UpdateLiveValues()
    {
        if (InvokeRequired)
        {
            BeginInvoke(UpdateLiveValues);
            return;
        }

        if (_app.ManualSource.IsSweeping)
        {
            var speed = _app.ManualSource.CurrentSpeedMph;
            _testSpeedSlider.Value = Math.Clamp((int)Math.Round(speed), _testSpeedSlider.Minimum, _testSpeedSlider.Maximum);
            _testSpeedLabel.Text = $"{speed:F0} mph (sweep)";
        }

        if (!_isLoading)
            _idleSpeedSlider.Value = (int)Math.Clamp(_app.Settings.IdleSpeedPercent, 0, 100);

        UpdateLabels();
        UpdateStatusHeader();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_capturingBinding != null && _capturingButton != null)
        {
            if (keyData == Keys.Escape)
            {
                _capturingBinding = null;
                _capturingButton.Text = _capturingButton.Tag as string ?? "Set";
                _capturingButton = null;
                return true;
            }

            if (keyData is Keys.Delete or Keys.Back)
            {
                _capturingBinding.Key = Keys.None;
                _capturingBinding.Control = false;
                _capturingBinding.Alt = false;
                _capturingBinding.Shift = false;
            }
            else
            {
                var key = keyData & Keys.KeyCode;
                if (key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey)
                    return true;

                _capturingBinding.Key = key;
                _capturingBinding.Control = keyData.HasFlag(Keys.Control);
                _capturingBinding.Alt = keyData.HasFlag(Keys.Alt);
                _capturingBinding.Shift = keyData.HasFlag(Keys.Shift);
            }

            _capturingButton.Text = _capturingBinding.DisplayText;
            _capturingButton.Tag = _capturingBinding.DisplayText;
            _capturingBinding = null;
            _capturingButton = null;
            PersistSettings();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void InitializeComponent()
    {
        Text = "Robs Wind Sim Settings";
        ClientSize = new Size(520, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(520, 520);

        _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200 };

        var header = CreateStatusHeader();
        header.Dock = DockStyle.Top;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateConnectionTab());
        tabs.TabPages.Add(CreateFansTab());
        tabs.TabPages.Add(CreateTestModeTab());
        tabs.TabPages.Add(CreateHotkeysTab());

        Controls.Add(tabs);
        Controls.Add(header);
    }

    private Panel CreateStatusHeader()
    {
        var panel = new Panel
        {
            Height = 100,
            Padding = new Padding(12, 10, 12, 8),
            BackColor = SystemColors.ControlLight
        };

        var title = new Label
        {
            Text = "Status",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(12, 10)
        };

        _liveOutputLabel = new Label
        {
            Text = "Live output — Left: 0%  Right: 0%",
            AutoSize = true,
            Location = new Point(12, 32)
        };

        _arduinoStatusLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 54)
        };

        _iracingStatusLabel = new Label
        {
            AutoSize = true,
            Location = new Point(220, 54)
        };

        _masterStatusLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 72)
        };

        panel.Controls.AddRange([title, _liveOutputLabel, _arduinoStatusLabel, _iracingStatusLabel, _masterStatusLabel]);
        return panel;
    }

    private TabPage CreateConnectionTab()
    {
        var page = new TabPage("Connection") { Padding = new Padding(12) };
        var table = CreateTwoColumnTable(2);

        _comPortCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _comPortCombo.SelectedIndexChanged += (_, _) => PersistSettings();

        var refreshButton = new Button { Text = "Refresh ports", AutoSize = true };
        refreshButton.Click += (_, _) =>
        {
            PopulateComPorts(_app.Settings.ComPort);
            PersistSettings();
        };

        var refreshPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };
        refreshPanel.Controls.Add(_comPortCombo);
        refreshPanel.Controls.Add(refreshButton);

        AddRow(table, 0, "COM port", refreshPanel);
        AddRow(table, 1, "Tip", new Label
        {
            Text = "Select the Arduino Nano port. Fans update within ~2 seconds of connecting.",
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = SystemColors.GrayText
        });

        page.Controls.Add(table);
        return page;
    }

    private TabPage CreateFansTab()
    {
        var page = new TabPage("Fans");

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        var mapping = CreateMappingSection();
        var channels = CreateChannelsSection();
        var idle = CreateIdleSection();

        const int sectionWidth = 472;
        mapping.SetBounds(0, 0, sectionWidth, mapping.Height);
        channels.SetBounds(0, mapping.Bottom + 8, sectionWidth, channels.Height);
        idle.SetBounds(0, channels.Bottom + 8, sectionWidth, idle.Height);

        scroll.Controls.AddRange([mapping, channels, idle]);
        scroll.AutoScrollMinSize = new Size(0, idle.Bottom + 16);
        scroll.Resize += (_, _) =>
        {
            var width = Math.Max(200, scroll.ClientSize.Width - 24);
            mapping.Width = width;
            channels.Width = width;
            idle.Width = width;
            channels.Top = mapping.Bottom + 8;
            idle.Top = channels.Bottom + 8;
        };

        page.Controls.Add(scroll);
        return page;
    }

    private static GroupBox CreateSectionGroup(string title, int height) => new()
    {
        Text = title,
        Height = height,
        Padding = new Padding(10, 22, 10, 8)
    };

    private GroupBox CreateMappingSection()
    {
        var group = CreateSectionGroup("Speed Mapping", 118);
        var table = CreateTwoColumnTable(3);
        table.Location = new Point(10, 18);

        _maxSpeedInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 400,
            DecimalPlaces = 0,
            Width = 100
        };
        _maxSpeedInput.ValueChanged += (_, _) =>
        {
            _testSpeedSlider.Maximum = (int)_maxSpeedInput.Value;
            PersistSettings();
        };

        _curveTypeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140
        };
        _curveTypeCombo.Items.AddRange(["Linear", "Exponential"]);
        _curveTypeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateCurveUiState();
            PersistSettings();
        };

        _curveExponentInput = new NumericUpDown
        {
            Minimum = 0.5m,
            Maximum = 5m,
            DecimalPlaces = 1,
            Increment = 0.1m,
            Width = 70
        };
        _curveExponentInput.ValueChanged += (_, _) => PersistSettings();

        AddRow(table, 0, "Max speed (mph)", _maxSpeedInput);
        AddRow(table, 1, "Curve type", _curveTypeCombo);
        AddRow(table, 2, "Exponent", _curveExponentInput);

        SetTip(_maxSpeedInput,
            "Vehicle speed that maps to 100% fan output. Example: at 200 mph, fans reach full power. Uses absolute mph, not per-car top speed.");
        SetTip(_curveTypeCombo,
            "Linear: fan power rises evenly with speed. Exponential: quieter at low speed, ramps up more aggressively near max speed.");
        SetTip(_curveExponentInput,
            "Shapes the exponential curve. Higher values (e.g. 2) keep fans quieter longer; 1 matches linear. Only applies when curve type is Exponential.");

        group.Controls.Add(table);
        return group;
    }

    private GroupBox CreateChannelsSection()
    {
        _channelsGroup = CreateSectionGroup("Channels", 198);

        _syncChannelsCheck = new CheckBox { Text = "Sync channels", AutoSize = true, Location = new Point(12, 24) };
        _leftEnabledCheck = new CheckBox { Text = "Left enabled", AutoSize = true, Location = new Point(140, 24) };
        _rightEnabledCheck = new CheckBox { Text = "Right enabled", AutoSize = true, Location = new Point(248, 24) };

        _leftMaxPowerSlider = CreatePercentSlider();
        _rightMaxPowerSlider = CreatePercentSlider();
        _leftMaxPowerLabel = new Label { AutoSize = true, Text = "100%" };
        _rightMaxPowerLabel = new Label { AutoSize = true, Text = "100%" };

        _leftMaxPowerPanel = CreateSliderBlock("Left max power", _leftMaxPowerSlider, _leftMaxPowerLabel, 52, out _leftMaxPowerCaption);
        _rightMaxPowerPanel = CreateSliderBlock("Right max power", _rightMaxPowerSlider, _rightMaxPowerLabel, 108, out _);

        SetTip(_syncChannelsCheck,
            "Drive both fan channels together with one max-power setting. Left/right enable toggles are hidden while synced.");
        SetTip(_leftEnabledCheck, "Enable or disable the left fan channel (pin D9).");
        SetTip(_rightEnabledCheck, "Enable or disable the right fan channel (pin D10).");
        SetTip(_leftMaxPowerPanel,
            "Scales the maximum output for this channel. 100% uses the full mapped speed output; lower values cap fan intensity.");
        SetTip(_rightMaxPowerPanel,
            "Scales the maximum output for the right channel independently of the left.");

        _syncChannelsCheck.CheckedChanged += (_, _) =>
        {
            if (_syncChannelsCheck.Checked)
                _rightMaxPowerSlider.Value = _leftMaxPowerSlider.Value;

            UpdateChannelUiState();
            PersistSettings();
        };
        _leftEnabledCheck.CheckedChanged += (_, _) => PersistSettings();
        _rightEnabledCheck.CheckedChanged += (_, _) => PersistSettings();
        _leftMaxPowerSlider.ValueChanged += (_, _) =>
        {
            if (_syncChannelsCheck.Checked)
                _rightMaxPowerSlider.Value = _leftMaxPowerSlider.Value;

            UpdateLabels();
            PersistSettings();
        };
        _rightMaxPowerSlider.ValueChanged += (_, _) => { UpdateLabels(); PersistSettings(); };

        _channelsGroup.Controls.AddRange([
            _syncChannelsCheck, _leftEnabledCheck, _rightEnabledCheck,
            _leftMaxPowerPanel, _rightMaxPowerPanel
        ]);
        return _channelsGroup;
    }

    private GroupBox CreateIdleSection()
    {
        var group = CreateSectionGroup("Idle Speed", 128);

        _idleSpeedSlider = CreatePercentSlider();
        _idleSpeedLabel = new Label { AutoSize = true, Text = "17%" };
        _idleStepInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 25,
            DecimalPlaces = 0,
            Width = 70,
            Location = new Point(142, 88)
        };

        var idleSlider = CreateSliderBlock("Idle fan speed", _idleSpeedSlider, _idleSpeedLabel, 24, out _);
        var stepLabel = new Label
        {
            Text = "Hotkey step (%)",
            AutoSize = true,
            Location = new Point(12, 92)
        };

        _idleSpeedSlider.ValueChanged += (_, _) => { UpdateLabels(); PersistSettings(); };
        _idleStepInput.ValueChanged += (_, _) => PersistSettings();

        SetTip(_idleSpeedSlider,
            "Fan speed when vehicle speed is 0 mph (pit, stop, or test mode at 0). Replaces mapped speed output, not added on top.");
        SetTip(_idleStepInput, "How much the idle speed hotkeys (F10/F11 by default) change idle fan speed per press.");

        group.Controls.AddRange([idleSlider, stepLabel, _idleStepInput]);
        return group;
    }

    private static Panel CreateSliderBlock(string caption, TrackBar slider, Label valueLabel, int top, out Label captionLabel)
    {
        var panel = new Panel
        {
            Left = 8,
            Top = top,
            Width = 450,
            Height = 52
        };

        captionLabel = new Label
        {
            Text = caption,
            AutoSize = true,
            Location = new Point(4, 2)
        };

        valueLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        valueLabel.Location = new Point(panel.Width - 48, 2);

        slider.Location = new Point(4, 22);
        slider.Width = panel.Width - 8;
        slider.Height = 32;
        slider.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        panel.Resize += (_, _) =>
        {
            valueLabel.Left = panel.ClientSize.Width - valueLabel.PreferredWidth - 4;
            slider.Width = panel.ClientSize.Width - 8;
        };

        panel.Controls.AddRange([captionLabel, valueLabel, slider]);
        return panel;
    }

    private void SetTip(Control control, string text) => _toolTip.SetToolTip(control, text);

    private TabPage CreateTestModeTab()
    {
        var page = new TabPage("Test Mode") { Padding = new Padding(12) };
        var table = CreateTwoColumnTable(3);

        _testModeCheck = new CheckBox
        {
            Text = "Use manual test speed instead of iRacing",
            AutoSize = true
        };
        _testModeCheck.CheckedChanged += (_, _) => PersistSettings();

        _testSpeedSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 200,
            TickFrequency = 20,
            Width = 300,
            Height = 40
        };
        _testSpeedLabel = new Label { AutoSize = true, Text = "0 mph" };

        _runSweepButton = new Button { Text = "Run test sweep (0 → max → 0)", AutoSize = true };
        _runSweepButton.Click += (_, _) => _app.StartTestSweep();

        _testSpeedSlider.ValueChanged += (_, _) =>
        {
            _app.ManualSource.SetTestSpeed(_testSpeedSlider.Value);
            UpdateLabels();
            PersistSettings();
        };

        AddRow(table, 0, "Mode", _testModeCheck, columnSpan: 2);

        var testSliderBlock = CreateSliderBlock("Test speed", _testSpeedSlider, _testSpeedLabel, 0, out _);
        testSliderBlock.Dock = DockStyle.Top;
        table.Controls.Add(testSliderBlock, 0, 1);
        table.SetColumnSpan(testSliderBlock, 2);
        table.RowStyles[1] = new RowStyle(SizeType.Absolute, 58);

        AddRow(table, 2, "Sweep", _runSweepButton);

        var hint = new Label
        {
            Text = "Idle speed (Fans tab) applies when test speed is 0 mph.",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 0),
            Dock = DockStyle.Top
        };

        var container = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
        container.Controls.Add(table, 0, 0);
        container.Controls.Add(hint, 0, 1);
        page.Controls.Add(container);
        return page;
    }

    private TabPage CreateHotkeysTab()
    {
        var page = new TabPage("Hotkeys") { Padding = new Padding(12) };

        var hint = new Label
        {
            Text = "Click Set, then press a key combination. Press Esc to cancel.",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8)
        };

        var table = CreateTwoColumnTable(4);
        _masterHotkeyButton = CreateHotkeyButton(_app.Settings.MasterToggleHotkey);
        _idleUpHotkeyButton = CreateHotkeyButton(_app.Settings.IdleUpHotkey);
        _idleDownHotkeyButton = CreateHotkeyButton(_app.Settings.IdleDownHotkey);

        AddRow(table, 0, "Master On/Off", _masterHotkeyButton);
        AddRow(table, 1, "Idle speed up", _idleUpHotkeyButton);
        AddRow(table, 2, "Idle speed down", _idleDownHotkeyButton);
        AddRow(table, 3, "Tip", new Label
        {
            Text = "These work system-wide, including while iRacing is focused.",
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = SystemColors.GrayText
        }, columnSpan: 2);

        table.Dock = DockStyle.Top;
        page.Controls.Add(table);
        page.Controls.Add(hint);
        return page;
    }

    private Button CreateHotkeyButton(HotkeyBinding binding)
    {
        var button = new Button
        {
            Text = binding.DisplayText,
            Tag = binding.DisplayText,
            Width = 160,
            Anchor = AnchorStyles.Left
        };

        button.Click += (_, _) =>
        {
            _capturingBinding = binding;
            _capturingButton = button;
            button.Text = "Press key...";
        };

        return button;
    }

    private static TableLayoutPanel CreateTwoColumnTable(int rows)
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = rows,
            Width = 430
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        for (var i = 0; i < rows; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        return table;
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control control, int columnSpan = 1)
    {
        if (!string.IsNullOrEmpty(labelText))
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 6)
            };
            table.Controls.Add(label, 0, row);
        }

        control.Margin = new Padding(0, 3, 0, 3);
        control.Dock = DockStyle.None;
        table.Controls.Add(control, string.IsNullOrEmpty(labelText) ? 0 : 1, row);

        if (columnSpan > 1)
            table.SetColumnSpan(control, columnSpan);
    }

    private static TrackBar CreatePercentSlider() => new()
    {
        Minimum = 0,
        Maximum = 100,
        TickFrequency = 10
    };

    private void PopulateComPorts(string? selectedPort)
    {
        var ports = SerialFanController.GetAvailablePorts();
        _comPortCombo.Items.Clear();
        _comPortCombo.Items.AddRange(ports);

        if (!string.IsNullOrWhiteSpace(selectedPort) && ports.Contains(selectedPort))
            _comPortCombo.SelectedItem = selectedPort;
        else if (_comPortCombo.Items.Count > 0)
            _comPortCombo.SelectedIndex = 0;
    }

    private void PersistSettings()
    {
        if (_isLoading)
            return;

        var settings = _app.Settings;
        settings.ComPort = _comPortCombo.SelectedItem as string ?? string.Empty;
        settings.MaxSpeedMph = (double)_maxSpeedInput.Value;
        settings.CurveType = Enum.Parse<CurveType>(_curveTypeCombo.SelectedItem?.ToString() ?? nameof(CurveType.Linear));
        settings.CurveExponent = (double)_curveExponentInput.Value;
        settings.LeftChannelEnabled = _syncChannelsCheck.Checked ? true : _leftEnabledCheck.Checked;
        settings.RightChannelEnabled = _syncChannelsCheck.Checked ? true : _rightEnabledCheck.Checked;
        settings.LeftMaxPowerPercent = _leftMaxPowerSlider.Value;
        settings.RightMaxPowerPercent = _syncChannelsCheck.Checked
            ? _leftMaxPowerSlider.Value
            : _rightMaxPowerSlider.Value;
        settings.SyncChannels = _syncChannelsCheck.Checked;
        settings.IdleSpeedPercent = _idleSpeedSlider.Value;
        settings.IdleStepPercent = (double)_idleStepInput.Value;
        settings.TestModeEnabled = _testModeCheck.Checked;
        settings.TestSpeedMph = _testSpeedSlider.Value;
        _app.ManualSource.SetTestSpeed(_testSpeedSlider.Value);
        _app.SaveSettings();
        UpdateStatusHeader();
    }

    private void UpdateCurveUiState()
    {
        var exponential = _curveTypeCombo.SelectedItem?.ToString() == CurveType.Exponential.ToString();
        _curveExponentInput.Enabled = exponential;
    }

    private void UpdateChannelUiState()
    {
        var sync = _syncChannelsCheck.Checked;

        _leftEnabledCheck.Enabled = !sync;
        _rightEnabledCheck.Enabled = !sync;

        if (sync)
        {
            _leftEnabledCheck.Checked = true;
            _rightEnabledCheck.Checked = true;
            _leftMaxPowerCaption.Text = "Fans max power";
            _rightMaxPowerPanel.Visible = false;
            _channelsGroup.Height = 142;
            SetTip(_leftMaxPowerPanel,
                "Scales maximum output for both fan channels together. 100% uses the full mapped speed output.");
        }
        else
        {
            _leftMaxPowerCaption.Text = "Left max power";
            _rightMaxPowerPanel.Visible = true;
            _channelsGroup.Height = 198;
            SetTip(_leftMaxPowerPanel,
                "Scales the maximum output for the left channel. 100% uses the full mapped speed output.");
        }
    }

    private void UpdateLabels()
    {
        _leftMaxPowerLabel.Text = $"{_leftMaxPowerSlider.Value}%";
        _rightMaxPowerLabel.Text = _syncChannelsCheck.Checked
            ? $"{_leftMaxPowerSlider.Value}%"
            : $"{_rightMaxPowerSlider.Value}%";
        _idleSpeedLabel.Text = $"{_idleSpeedSlider.Value}%";
        _testSpeedLabel.Text = $"{_testSpeedSlider.Value} mph";
    }

    private void UpdateStatusHeader()
    {
        var output = _app.LastOutput;
        _liveOutputLabel.Text = $"Live output — Left: {output.LeftPercent:F0}%  Right: {output.RightPercent:F0}%";

        _arduinoStatusLabel.Text = _app.Serial.IsConnected
            ? $"Arduino: Connected ({_app.Serial.ConfiguredPort})"
            : $"Arduino: Disconnected{(string.IsNullOrWhiteSpace(_app.Settings.ComPort) ? "" : $" ({_app.Settings.ComPort})")}";

        _iracingStatusLabel.Text = _app.IracingSource.IsConnected
            ? "iRacing: Connected"
            : "iRacing: Not connected (phase 2)";

        _masterStatusLabel.Text = _app.Settings.MasterEnabled
            ? "Master output: On"
            : "Master output: Off";
    }

    private void UpdateHotkeyButtons()
    {
        var settings = _app.Settings;
        _masterHotkeyButton.Text = settings.MasterToggleHotkey.DisplayText;
        _idleUpHotkeyButton.Text = settings.IdleUpHotkey.DisplayText;
        _idleDownHotkeyButton.Text = settings.IdleDownHotkey.DisplayText;
    }
}
