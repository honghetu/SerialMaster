using System.Windows;

namespace SerialMaster.UI.Themes;

public class ThemeService
{
    public string CurrentTheme { get; private set; } = "Dark";

    public void ApplyTheme(string theme)
    {
        CurrentTheme = theme;
        string themePath = theme == "Light" ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";

        var dict = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
    }

    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
    }
}
