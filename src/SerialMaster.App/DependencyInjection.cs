using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using SerialMaster.UI.ViewModels;

namespace SerialMaster.App;

public static class DependencyInjection
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IDeviceEnumerator, DeviceEnumerator>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddTransient<ISerialPortService, SerialPortService>();

        // UI services
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddSingleton<DeviceManagerViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
