using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Services;
using SerialMaster.UI.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class MacroEditorViewModel : ObservableObject
{
    private readonly Func<byte[], Task> _sendCallback;
    private readonly Action _closeCallback;
    private CancellationTokenSource? _playCts;

    [ObservableProperty]
    private ObservableCollection<MacroStep> _steps = new();

    [ObservableProperty]
    private MacroStep? _selectedStep;

    [ObservableProperty]
    private MacroStep.StepType _newStepType = MacroStep.StepType.Send;

    [ObservableProperty]
    private string _newStepData = string.Empty;

    [ObservableProperty]
    private int _newStepDelay = 100;

    [ObservableProperty]
    private int _newStepRepeat = 1;

    [ObservableProperty]
    private string _newStepLabel = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _tabTitle = "⚙ 宏编辑器";

    public MacroEditorViewModel(Func<byte[], Task> sendCallback, Action closeCallback)
    {
        _sendCallback = sendCallback;
        _closeCallback = closeCallback;
    }

    public void Close()
    {
        _playCts?.Cancel();
        _closeCallback();
    }

    [RelayCommand]
    private void AddStep()
    {
        var step = new MacroStep
        {
            Type = NewStepType,
            Data = NewStepData,
            DelayMs = NewStepDelay,
            RepeatCount = NewStepRepeat,
            Label = NewStepLabel
        };
        Steps.Add(step);
    }

    [RelayCommand]
    private void DeleteStep(MacroStep? step)
    {
        if (step != null) Steps.Remove(step);
    }

    [RelayCommand]
    private void MoveUp(MacroStep? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx > 0) Steps.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown(MacroStep? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx < Steps.Count - 1) Steps.Move(idx, idx + 1);
    }

    [RelayCommand]
    private async Task Play()
    {
        if (Steps.Count == 0) return;
        IsPlaying = true;
        _playCts = new CancellationTokenSource();

        try
        {
            var loopStack = new Stack<(int stepIndex, int remaining)>();
            int i = 0;
            while (i < Steps.Count)
            {
                _playCts.Token.ThrowIfCancellationRequested();
                var step = Steps[i];

                switch (step.Type)
                {
                    case MacroStep.StepType.Send:
                        if (!string.IsNullOrWhiteSpace(step.Data))
                        {
                            // Use InputParser so "0xAA BB CC" / "AA-BB" / mixed case all work.
                            var data = InputParser.ParseHex(step.Data);
                            if (data.Length > 0) await _sendCallback(data);
                        }
                        if (step.DelayMs > 0)
                            await Task.Delay(step.DelayMs, _playCts.Token);
                        break;

                    case MacroStep.StepType.Delay:
                        await Task.Delay(step.DelayMs, _playCts.Token);
                        break;

                    case MacroStep.StepType.LoopStart:
                        loopStack.Push((i, step.RepeatCount));
                        break;

                    case MacroStep.StepType.LoopEnd:
                        if (loopStack.Count > 0)
                        {
                            var loop = loopStack.Peek();
                            if (loop.remaining > 1)
                            {
                                loopStack.Pop();
                                loopStack.Push((loop.stepIndex, loop.remaining - 1));
                                i = loop.stepIndex;
                                continue;
                            }
                            else
                                loopStack.Pop();
                        }
                        break;
                }
                i++;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsPlaying = false;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _playCts?.Cancel();
        IsPlaying = false;
    }

    [RelayCommand]
    private void Save()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存宏",
            Filter = "宏文件 (*.macro)|*.macro|所有文件 (*.*)|*.*",
            DefaultExt = ".macro"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = Steps.Select(s =>
                $"{(int)s.Type}|{s.Data}|{s.DelayMs}|{s.RepeatCount}|{s.Label}");
            File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
            MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Load()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "加载宏",
            Filter = "宏文件 (*.macro)|*.macro|所有文件 (*.*)|*.*",
            DefaultExt = ".macro"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Steps.Clear();
            foreach (var line in File.ReadAllLines(dlg.FileName))
            {
                var parts = line.Split('|');
                if (parts.Length >= 4 && Enum.TryParse<MacroStep.StepType>(parts[0], out var type))
                {
                    Steps.Add(new MacroStep
                    {
                        Type = type,
                        Data = parts[1],
                        DelayMs = int.TryParse(parts[2], out var d) ? d : 100,
                        RepeatCount = int.TryParse(parts[3], out var r) ? r : 1,
                        Label = parts.Length > 4 ? parts[4] : ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
