using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SerialMaster.UI.Converters;

public class SendStatusKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value?.ToString() switch
        {
            "Success" => "SuccessBrush",
            "Warning" => "WarningBrush",
            "Error"   => "ErrorBrush",
            "Info"    => "AccentBrush",
            _         => "MutedTextBrush"
        };
        return Application.Current?.TryFindResource(key) as Brush
               ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
