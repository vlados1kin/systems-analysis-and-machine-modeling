namespace CAIMMOD.Laba3;

public class SimulationParameters
{
    public int NumTables { get; init; } = 15;
    public int NumWaiters { get; init; } = 3;
    public double MeanInterarrivalMin { get; set; } = 2.0;
    public double MeanServiceMin { get; init; } = 20.0;
    public double SimDurationMin { get; set; } = 600.0;
    public int Runs { get; set; } = 30;
    public int Seed { get; init; } = 12345;
    public static bool WaitIfNoWaiter => true;
    public static int GroupMinSize => 1;
    public static int GroupMaxSize => 4;
}