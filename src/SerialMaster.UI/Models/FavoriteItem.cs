namespace SerialMaster.UI.Models;

public class FavoriteItem
{
    public string Name { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Category { get; set; } = "默认";
    public bool IsHex { get; set; } = true;
}
