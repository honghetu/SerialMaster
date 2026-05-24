using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SerialMaster.UI.Controls;

public partial class StatusIndicator : UserControl
{
    public static readonly DependencyProperty StatusBrushProperty =
        DependencyProperty.Register(nameof(StatusBrush), typeof(Brush), typeof(StatusIndicator),
            new PropertyMetadata(Brushes.Gray));

    public Brush StatusBrush
    {
        get => (Brush)GetValue(StatusBrushProperty);
        set => SetValue(StatusBrushProperty, value);
    }

    public StatusIndicator()
    {
        InitializeComponent();
    }
}
