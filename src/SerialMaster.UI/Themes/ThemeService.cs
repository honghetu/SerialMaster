using System.Windows;

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
    }

    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
    }
}
