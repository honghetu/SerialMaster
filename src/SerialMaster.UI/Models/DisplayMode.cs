namespace SerialMaster.UI.Models;

public enum DisplayMode
{
    /// <summary>每条记录显示为 HEX 字节</summary>
    Hex,
    /// <summary>每条记录显示为 ASCII (不可打印字符显示为 .)</summary>
    Ascii,
    /// <summary>同一行左侧 HEX 右侧 ASCII 对照</summary>
    Dual,
    /// <summary>连续 VT 终端样式（ASCII 流，不分条目）</summary>
    Terminal
}
