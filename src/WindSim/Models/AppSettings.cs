using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace WindSim.Models;

public enum CurveType
{
    Linear,
    Exponential
}

public sealed class HotkeyBinding
{
    public Keys Key { get; set; } = Keys.None;
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }

    public uint Modifiers =>
        (Control ? 0x0002u : 0u) |
        (Alt ? 0x0001u : 0u) |
        (Shift ? 0x0004u : 0u);

    public string DisplayText
    {
        get
        {
            if (Key == Keys.None)
                return "(none)";

            var parts = new List<string>();
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }
}

public sealed class AppSettings
{
    private const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; }
    public string ComPort { get; set; } = string.Empty;
    public double MaxSpeedMph { get; set; } = 200;
    public CurveType CurveType { get; set; } = CurveType.Linear;
    public double CurveExponent { get; set; } = 2.0;
    public bool LeftChannelEnabled { get; set; } = true;
    public bool RightChannelEnabled { get; set; } = true;
    public double LeftMaxPowerPercent { get; set; } = 100;
    public double RightMaxPowerPercent { get; set; } = 100;
    public bool SyncChannels { get; set; } = true;
    public double IdleSpeedPercent { get; set; } = 17;
    public double IdleStepPercent { get; set; } = 5;
    public bool TestModeEnabled { get; set; } = true;
    public double TestSpeedMph { get; set; }
    public bool MasterEnabled { get; set; } = true;
    public HotkeyBinding MasterToggleHotkey { get; set; } = new() { Key = Keys.F9 };
    public HotkeyBinding IdleUpHotkey { get; set; } = new() { Key = Keys.F10 };
    public HotkeyBinding IdleDownHotkey { get; set; } = new() { Key = Keys.F11 };

    [JsonIgnore]
    public static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        AppSettings settings;
        var isNewInstall = !File.Exists(SettingsPath);

        try
        {
            if (isNewInstall)
            {
                settings = CreateDefaults();
            }
            else
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? CreateDefaults();
            }
        }
        catch
        {
            settings = CreateDefaults();
        }

        settings.Migrate(isNewInstall);
        return settings;
    }

    public void Save()
    {
        SchemaVersion = CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(this, JsonOptions());
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings CreateDefaults() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        LeftMaxPowerPercent = 100,
        RightMaxPowerPercent = 100
    };

    private void Migrate(bool isNewInstall)
    {
        if (isNewInstall || SchemaVersion >= CurrentSchemaVersion)
            return;

        if (LeftMaxPowerPercent <= 0)
            LeftMaxPowerPercent = 100;

        if (RightMaxPowerPercent <= 0)
            RightMaxPowerPercent = 100;

        SchemaVersion = CurrentSchemaVersion;
        Save();
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
