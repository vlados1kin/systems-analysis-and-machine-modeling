namespace CAIMMOD.Laba2;

public class SimulationParameters
{
    public int NumTables { get; init; } = 15;
    public int NumWaiters { get; init; } = 3;
    public double MeanInterarrivalMin { get; init; } = 2.0;
    public double MeanServiceMin { get; init; } = 20.0;
    public double SimDurationMin { get; init; } = 600.0;
    public int Runs { get; init; } = 30;
    public static bool WaitIfNoWaiter => true;
    public static int Seed => 12345;
    public static int GroupMinSize => 1;
    public static int GroupMaxSize => 4;
}