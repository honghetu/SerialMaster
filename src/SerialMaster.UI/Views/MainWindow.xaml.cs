using System.Windows;
using SerialMaster.UI.ViewModels;

namespace SerialMaster.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
