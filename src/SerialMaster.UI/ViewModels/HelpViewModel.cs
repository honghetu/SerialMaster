using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace SerialMaster.UI.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    private readonly Action _closeCallback;

    [ObservableProperty]
    private string _tabTitle = "📘 使用说明";

    [ObservableProperty]
    private string _version = "?";

    public HelpViewModel(Action closeCallback)
    {
        _closeCallback = closeCallback;
        // GetEntryAssembly = SerialMaster.App (the .exe), which carries the real Version.
        // GetExecutingAssembly here returns SerialMaster.UI (defaults to 1.0).
        Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
                  ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                  ?? "?";
    }

    public void Close() => _closeCallback();
}
