using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using SerialMaster.UI.ViewModels;
using SerialMaster.UI.Views;

namespace SerialMaster.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _serviceProvider = DependencyInjection.ConfigureServices();

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsService.Load();

        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        themeService.ApplyTheme(settings.Theme);

        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();
    }
}
