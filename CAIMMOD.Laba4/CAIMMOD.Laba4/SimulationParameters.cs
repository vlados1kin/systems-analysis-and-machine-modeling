using CAIMMOD.Laba4.Models;

namespace CAIMMOD.Laba4;

public class SimulationParameters
{
    public int NumTables { get; init; } = 15;
    public int NumWaiters { get; set; } = 3;
    public double MeanInterarrivalMin { get; set; } = 2.0;
    public double MeanServiceMin { get; set; } = 20.0;
    public double SimDurationMin { get; set; } = 600.0;
    public int Runs { get; set; } = 30;
    public int Seed { get; init; } = 12345;
    public static bool WaitIfNoWaiter => true;
    public static int GroupMinSize => 1;
    public static int GroupMaxSize => 4;
    public QueueDiscipline Discipline { get; set; } = QueueDiscipline.Fifo;
    public double? ReduceWaiterTime { get; set; }
}