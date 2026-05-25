using System.Runtime.InteropServices;

namespace SerialMaster.UI.Helpers;

public static class WindowFrameHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void SetDarkMode(IntPtr hwnd, bool dark)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDark, sizeof(int));
        }
    }
}
