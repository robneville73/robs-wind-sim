namespace RobsWindSim.Models;

public readonly struct FanOutput
{
    public double LeftPercent { get; }
    public double RightPercent { get; }

    public FanOutput(double leftPercent, double rightPercent)
    {
        LeftPercent = leftPercent;
        RightPercent = rightPercent;
    }

    public static FanOutput Zero => new(0, 0);

    public bool IsActive => LeftPercent > 0.01 || RightPercent > 0.01;
}
