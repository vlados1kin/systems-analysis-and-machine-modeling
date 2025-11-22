namespace CAIMMOD.Laba2.Models;

public class Event(double time, EventType type, Group? group = null, Waiter? assignedWaiter = null) : IComparable<Event>
{
    public double Time { get; init; } = time;
    public EventType Type { get; init; } = type;
    public Group? Group { get; init; } = group;
    public Waiter? AssignedWaiter { get; init; } = assignedWaiter;

    public int CompareTo(Event? other)
    {
        if (other == null) return -1;
        if (Time < other.Time) return -1;
        if (Time > other.Time) return 1;
        return Type.CompareTo(other.Type);
    }
}