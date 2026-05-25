using System.Windows;

namespace SerialMaster.UI.Windows;

public partial class WaveformWindow : Window
{
    public WaveformWindow()
    {
        InitializeComponent();
    }

    public void AddDataPoint(int channel, float value)
    {
        if (WaveformCanvas == null) return;
        if (channel >= WaveformCanvas.ChannelCount)
            WaveformCanvas.ChannelCount = channel + 1;
        WaveformCanvas.AddDataPoint(channel, value);
    }

    public void Refresh() => WaveformCanvas.Refresh();

    private void OnChannelCountChanged(object sender, RoutedEventArgs e)
    {
        if (WaveformCanvas == null) return;
        if (int.TryParse(ChannelCountBox.Text, out int count) && count > 0 && count <= 12)
            WaveformCanvas.ChannelCount = count;
    }
}
