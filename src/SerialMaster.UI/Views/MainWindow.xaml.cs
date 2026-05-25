using System.Windows;
using System.Windows.Interop;
using SerialMaster.UI.Helpers;
using SerialMaster.UI.ViewModels;

namespace SerialMaster.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var vm = (MainViewModel)DataContext;
        WindowFrameHelper.SetDarkMode(handle, !vm.IsLightTheme);
    }
}
