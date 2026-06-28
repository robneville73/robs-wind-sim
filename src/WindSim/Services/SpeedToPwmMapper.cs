using WindSim.Models;

namespace WindSim.Services;

public static class SpeedToPwmMapper
{
    public static double MapSpeedToPercent(double speedMph, AppSettings settings)
    {
        if (speedMph <= 0 || settings.MaxSpeedMph <= 0)
            return 0;

        var normalized = Math.Clamp(speedMph / settings.MaxSpeedMph, 0, 1);

        return settings.CurveType switch
        {
            CurveType.Exponential => Math.Pow(normalized, settings.CurveExponent) * 100,
            _ => normalized * 100
        };
    }
}
