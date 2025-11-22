namespace CAIMMOD.Laba3.Models;

public class Group(int id, int size, double arrivalTime, double serviceStartTime = double.NaN)
{
    public int Id { get; } = id;
    public int Size { get; } = size;
    public double ArrivalTime { get; } = arrivalTime;
    public double ServiceStartTime { get; set; } = serviceStartTime;
}