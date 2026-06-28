namespace RobsWindSim.Services;

public interface ISpeedSource
{
    string Name { get; }
    bool IsConnected { get; }
    double CurrentSpeedMph { get; }
}
