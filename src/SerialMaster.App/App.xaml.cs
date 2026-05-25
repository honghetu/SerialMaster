using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SerialMaster.Core.Services;
using SerialMaster.UI.Themes;
using SerialMaster.UI.ViewModels;
using SerialMaster.UI.Views;

namespace SerialMaster.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject.ToString()!));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            _serviceProvider = DependencyInjection.ConfigureServices();

            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.Load();

            var themeService = _serviceProvider.GetRequiredService<ThemeService>();
            themeService.ApplyTheme(settings.Theme);

            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainViewModel);
            mainWindow.Show();

            // Non-blocking background check; only prompts if a newer release exists.
            _ = mainViewModel.CheckForUpdatesSilentAsync();
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        // Show one-line message to user instead of silently swallowing — they should know.
        MessageBox.Show(
            $"发生未处理异常 (已记入日志):\n\n{e.Exception.Message}\n\n详情见 %LocalAppData%\\SerialMaster\\logs\\",
            "SerialMaster — 错误",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogCrash(Exception ex)
    {
        // Unified via FileLogger (writes daily file in /logs/).
        FileLogger.Error("Unhandled exception", ex);
    }
}
