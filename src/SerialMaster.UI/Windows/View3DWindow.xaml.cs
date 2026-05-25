using System.Windows;

namespace SerialMaster.UI.Windows;

public partial class View3DWindow : Window
{
    public View3DWindow()
    {
        InitializeComponent();
    }

    public void AddPoint(double x, double y, double z, uint color = 0xFFA6E3A1)
    {
        View3DCanvas.AddPoint(x, y, z, color);
    }

    public void Clear()
    {
        View3DCanvas.Clear();
    }
}
