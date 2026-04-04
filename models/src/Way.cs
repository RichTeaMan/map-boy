namespace MapBoy.Models;

public class Way
{
    public long Id { get; set; }
    public bool? Visible { get; set; }
    public long? Uid { get; set; }
    public bool ClosedLoop { get; set; }
    public long? AreaParentId { get; set; }
    public string Tags { get; set; } = "";
}
