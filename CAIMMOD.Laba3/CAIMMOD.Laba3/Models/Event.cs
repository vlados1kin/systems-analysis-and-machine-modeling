namespace CAIMMOD.Laba3.Models;

public class Event(double time, EventType type, Group? group = null, Waiter? assignedWaiter = null) : IComparable<Event>
{
    public double Time { get; } = time;
    public EventType Type { get; } = type;
    public Group? Group { get; } = group;
    public Waiter? AssignedWaiter { get; } = assignedWaiter;

    public int CompareTo(Event? other)
    {
        if (other == null) return -1;
        if (Time < other.Time) return -1;
        if (Time > other.Time) return 1;
        return Type.CompareTo(other.Type);
    }
}