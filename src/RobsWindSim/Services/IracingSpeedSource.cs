namespace RobsWindSim.Services;

public sealed class IracingSpeedSource : ISpeedSource
{
    public string Name => "iRacing";
    public bool IsConnected => false;
    public double CurrentSpeedMph => 0;
}
