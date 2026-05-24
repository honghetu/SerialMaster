namespace SerialMaster.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string LastPort { get; set; } = string.Empty;
    public int LastBaudRate { get; set; } = 115200;
    public List<SerialConfig> RecentConfigs { get; set; } = new();
}
