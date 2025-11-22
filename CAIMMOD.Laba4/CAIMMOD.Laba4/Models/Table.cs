namespace CAIMMOD.Laba4.Models;

public class Table
{
    public int Id { get; init; }
    public bool IsOccupied { get; set; }
    public Group? OccupyingGroup { get; set; }
}