namespace MyCMS2026.Models;

public class Role
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public int    SortOrder   { get; set; } = 10;
}
