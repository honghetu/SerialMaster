using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialMaster.UI.ViewModels;

public partial class PlaceholderSession : ObservableObject
{
    [ObservableProperty]
    private string _tabTitle;

    public string Description { get; }
    public string Phase { get; }
    public string Icon { get; }

    private readonly Action _onClose;

    public PlaceholderSession(string feature, string icon, string phase, string description, Action onClose)
    {
        _tabTitle = $"{icon} {feature}";
        Icon = icon;
        Phase = phase;
        Description = description;
        _onClose = onClose;
    }

    public void Close() => _onClose();
}
