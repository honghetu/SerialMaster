using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialMaster.Core.Services;
using System.Collections.ObjectModel;

namespace SerialMaster.UI.ViewModels;

public partial class ChecksumViewModel : ObservableObject
{
    private readonly Action _onClose;

    [ObservableProperty]
    private string _inputHex = string.Empty;

    [ObservableProperty]
    private string _resultXor = "—";

    [ObservableProperty]
    private string _resultAdd = "—";

    [ObservableProperty]
    private string _resultCrc8 = "—";

    [ObservableProperty]
    private string _resultCrc16 = "—";

    [ObservableProperty]
    private string _resultCrc32 = "—";

    [ObservableProperty]
    private string _resultModbus = "—";

    public string TabTitle => "🔒 校验计算器";

    public ChecksumViewModel(Action onClose)
    {
        _onClose = onClose;
    }

    [RelayCommand]
    private void Compute()
    {
        if (string.IsNullOrWhiteSpace(InputHex))
        {
            ResultXor = ResultAdd = ResultCrc8 = ResultCrc16 = ResultCrc32 = ResultModbus = "—";
            return;
        }

        ResultXor = ChecksumService.Compute(InputHex, ChecksumType.XOR);
        ResultAdd = ChecksumService.Compute(InputHex, ChecksumType.ADD);
        ResultCrc8 = ChecksumService.Compute(InputHex, ChecksumType.CRC8);
        ResultCrc16 = ChecksumService.Compute(InputHex, ChecksumType.CRC16);
        ResultCrc32 = ChecksumService.Compute(InputHex, ChecksumType.CRC32);
        ResultModbus = ChecksumService.Compute(InputHex, ChecksumType.ModbusCRC);
    }

    public void Close() => _onClose();
}
