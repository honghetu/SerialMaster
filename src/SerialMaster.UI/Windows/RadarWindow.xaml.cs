using System.Windows;

namespace SerialMaster.UI.Windows;

public partial class RadarWindow : Window
{
    public RadarWindow()
    {
        InitializeComponent();
    }

    public void SetValues(float[] values, string[] labels, float maxValue = 255f)
    {
        RadarCanvas.SetValues(values, labels, maxValue);
    }
}
