using RobsWindSim.Models;

namespace RobsWindSim.Services;

public static class FanOutputPipeline
{
    public static FanOutput Compute(AppSettings settings, ISpeedSource activeSource)
    {
        if (!settings.MasterEnabled)
            return FanOutput.Zero;

        var speedMph = activeSource.CurrentSpeedMph;

        double basePercent;
        if (speedMph <= 0)
            basePercent = settings.IdleSpeedPercent;
        else
            basePercent = SpeedToPwmMapper.MapSpeedToPercent(speedMph, settings);

        var leftPercent = settings.LeftChannelEnabled
            ? basePercent * (settings.LeftMaxPowerPercent / 100.0)
            : 0;

        var rightPercent = settings.SyncChannels
            ? leftPercent
            : settings.RightChannelEnabled
                ? basePercent * (settings.RightMaxPowerPercent / 100.0)
                : 0;

        return new FanOutput(
            Math.Clamp(leftPercent, 0, 100),
            Math.Clamp(rightPercent, 0, 100));
    }
}
