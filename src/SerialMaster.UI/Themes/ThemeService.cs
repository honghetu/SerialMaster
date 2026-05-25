using System.Windows;
using System.Windows.Interop;
using SerialMaster.UI.Helpers;

namespace SerialMaster.UI.Themes;

public class ThemeService
{
    public string CurrentTheme { get; private set; } = "Dark";

    public void ApplyTheme(string theme)
    {
        CurrentTheme = theme;
        string themeFile = theme == "Light" ? "LightTheme.xaml" : "DarkTheme.xaml";
        var uri = new Uri($"pack://application:,,,/SerialMaster.UI;component/Themes/{themeFile}");

        var dict = new ResourceDictionary
        {
            Source = uri
        };

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);

        ApplyWindowFrame(theme);
    }

    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
    }

    private static void ApplyWindowFrame(string theme)
    {
        if (Application.Current.MainWindow is { } window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                WindowFrameHelper.SetDarkMode(handle, theme != "Light");
            }
        }
    }
}
