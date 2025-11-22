namespace CAIMMOD.Laba2.Models;

public class Group(int id, int size, double arrivalTime, double serviceStartTime = double.NaN)
{
    public int Id { get; init; } = id;
    public int Size { get; init; } = size;
    public double ArrivalTime { get; init; } = arrivalTime;
    public double ServiceStartTime { get; set; } = serviceStartTime;
}