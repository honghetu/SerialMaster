using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using System.Collections.ObjectModel;

namespace SerialMaster.UI.ViewModels;

public partial class LogViewerSession : ObservableObject
{
    private readonly Action _onClose;

    [ObservableProperty]
    private string _tabTitle;

    public ObservableCollection<DataRecord> Records { get; }

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private bool _isPaused;

    public LogViewerSession(string fileName, ObservableCollection<DataRecord> records, Action onClose)
    {
        _tabTitle = $"📄 {fileName}";
        _onClose = onClose;
        Records = records;
        TotalBytes = records.Sum(r => (long)r.Data.Length);
    }

    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

    [RelayCommand]
    private void ClearRecords()
    {
        Records.Clear();
        TotalBytes = 0;
    }

    public void Close() => _onClose();
}
