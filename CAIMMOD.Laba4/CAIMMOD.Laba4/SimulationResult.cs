namespace CAIMMOD.Laba4;

public class SimulationResult
{
    public List<(double t, double occupiedFraction)> OccupiedTimeline = [];
    public List<(double t, double util)> UtilizationTimeline = [];
    public List<(double t, double avgWaitingDiscrete)> AvgWaitTimeline = [];
    public List<(double t, int served)> CumulativeServedTimeline = [];
    public List<(double Time, double Duration)> CustomerHistory { get; init; } = [];
    
    public double AvgWaitingTime;
    public double FractionLost;
    public double AvgUtilization;
    public int TotalArrivals;
    public int TotalLost;
    public int TotalServed;
}