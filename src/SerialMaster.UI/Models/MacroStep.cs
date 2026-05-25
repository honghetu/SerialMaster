using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialMaster.UI.Models;

public partial class MacroStep : ObservableObject
{
    public enum StepType { Send, Delay, LoopStart, LoopEnd, Label }

    [ObservableProperty]
    private StepType _type = StepType.Send;

    [ObservableProperty]
    private string _data = string.Empty;

    [ObservableProperty]
    private int _delayMs = 100;

    [ObservableProperty]
    private int _repeatCount = 1;

    [ObservableProperty]
    private string _label = string.Empty;
}
