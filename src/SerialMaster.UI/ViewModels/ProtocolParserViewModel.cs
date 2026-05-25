using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SerialMaster.UI.ViewModels;

public partial class ProtocolParserViewModel : ObservableObject
{
    private readonly IProtocolDefinitionStore _store;
    private readonly Func<SessionViewModel?> _activeSessionAccessor;
    private readonly Action _closeCallback;

    [ObservableProperty]
    private ObservableCollection<ProtocolDefinition> _definitions = new();

    [ObservableProperty]
    private ProtocolDefinition? _selectedDefinition;

    [ObservableProperty]
    private ObservableCollection<ProtocolField> _fields = new();

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editHeaderHex = string.Empty;

    [ObservableProperty]
    private int _editFrameLength = 0;

    [ObservableProperty]
    private string _inputHex = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ParseResultItem> _parseResults = new();

    [ObservableProperty]
    private string _tabTitle = "📦 协议解析器";

    public IReadOnlyList<FieldType> FieldTypes { get; } =
        Enum.GetValues<FieldType>();

    public ProtocolParserViewModel(
        IProtocolDefinitionStore store,
        Func<SessionViewModel?> activeSessionAccessor,
        Action closeCallback)
    {
        _store = store;
        _activeSessionAccessor = activeSessionAccessor;
        _closeCallback = closeCallback;

        foreach (var def in _store.LoadAll())
            Definitions.Add(def);

        if (Definitions.Count > 0)
            SelectedDefinition = Definitions[0];
    }

    public void Close() => _closeCallback();

    partial void OnSelectedDefinitionChanged(ProtocolDefinition? value)
    {
        if (value == null)
        {
            EditName = string.Empty;
            EditHeaderHex = string.Empty;
            EditFrameLength = 0;
            Fields.Clear();
            return;
        }

        EditName = value.Name;
        EditHeaderHex = value.HeaderHex;
        EditFrameLength = value.FrameLength;
        Fields.Clear();
        foreach (var f in value.Fields)
            Fields.Add(new ProtocolField { Name = f.Name, Offset = f.Offset, Length = f.Length, Type = f.Type });
    }

    [RelayCommand]
    private void NewDefinition()
    {
        var def = new ProtocolDefinition { Name = "新协议", FrameLength = 4 };
        Definitions.Add(def);
        SelectedDefinition = def;
    }

    [RelayCommand]
    private void AddField()
    {
        int nextOffset = Fields.Count == 0 ? 0 : Fields.Max(f => f.Offset + f.Length);
        Fields.Add(new ProtocolField
        {
            Name = $"字段{Fields.Count + 1}",
            Offset = nextOffset,
            Length = 1,
            Type = FieldType.UInt8
        });
    }

    [RelayCommand]
    private void DeleteField(ProtocolField? field)
    {
        if (field != null) Fields.Remove(field);
    }

    [RelayCommand]
    private void SaveDefinition()
    {
        if (SelectedDefinition == null) return;
        SelectedDefinition.Name = string.IsNullOrWhiteSpace(EditName) ? "(未命名)" : EditName;
        SelectedDefinition.HeaderHex = EditHeaderHex;
        SelectedDefinition.FrameLength = EditFrameLength;
        SelectedDefinition.Fields = Fields.ToList();

        _store.SaveAll(Definitions);
        MessageBox.Show($"已保存协议: {SelectedDefinition.Name}", "提示",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void DeleteDefinition()
    {
        if (SelectedDefinition == null) return;
        if (MessageBox.Show($"删除协议 \"{SelectedDefinition.Name}\"?", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        Definitions.Remove(SelectedDefinition);
        _store.SaveAll(Definitions);
        SelectedDefinition = Definitions.FirstOrDefault();
    }

    [RelayCommand]
    private void ApplyToActiveSession()
    {
        var session = _activeSessionAccessor();
        if (session == null)
        {
            MessageBox.Show("没有活动的串口会话", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (SelectedDefinition == null) return;

        SaveDefinition();
        session.ApplyProtocol(CloneDefinition(SelectedDefinition));
        MessageBox.Show($"已应用 \"{SelectedDefinition.Name}\" 到会话 {session.TabTitle}", "提示",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static ProtocolDefinition CloneDefinition(ProtocolDefinition src) => new()
    {
        Name = src.Name,
        HeaderHex = src.HeaderHex,
        FrameLength = src.FrameLength,
        Fields = src.Fields.Select(f => new ProtocolField
        {
            Name = f.Name, Offset = f.Offset, Length = f.Length, Type = f.Type
        }).ToList()
    };

    [RelayCommand]
    private void Parse()
    {
        ParseResults.Clear();
        if (string.IsNullOrWhiteSpace(InputHex) || SelectedDefinition == null) return;

        var bytes = InputParser.ParseHex(InputHex);
        ParseResults.Add(new ParseResultItem("原始数据",
            Convert.ToHexString(bytes).InsertSpacesEvery2(), false));

        var parser = new ProtocolParser
        {
            Definition = new ProtocolDefinition
            {
                Name = SelectedDefinition.Name,
                HeaderHex = EditHeaderHex,
                FrameLength = EditFrameLength,
                Fields = Fields.ToList()
            }
        };

        int frameCount = 0;
        foreach (var frame in parser.Feed(bytes))
        {
            frameCount++;
            ParseResults.Add(new ParseResultItem($"— 帧 #{frameCount} —", "", false));
            foreach (var v in frame.FieldValues)
                ParseResults.Add(new ParseResultItem(v.Name, v.Value, !v.IsError));
        }

        if (frameCount == 0)
            ParseResults.Add(new ParseResultItem("提示", "未解析出完整帧（检查帧头/长度）", false));
    }
}

public class ParseResultItem
{
    public string Name { get; }
    public string Value { get; }
    public bool IsSuccess { get; }

    public ParseResultItem(string name, string value, bool isSuccess)
    {
        Name = name;
        Value = value;
        IsSuccess = isSuccess;
    }
}

internal static class HexStringExt
{
    public static string InsertSpacesEvery2(this string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= 2) return s;
        var sb = new System.Text.StringBuilder(s.Length + s.Length / 2);
        for (int i = 0; i < s.Length; i += 2)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(s, i, 2);
        }
        return sb.ToString();
    }
}
