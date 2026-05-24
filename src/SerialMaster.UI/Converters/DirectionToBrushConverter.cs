using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SerialMaster.Core.Models;

namespace SerialMaster.UI.Converters;

public class DirectionToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataDirection dir)
        {
            string key = dir == DataDirection.Send ? "SendDirectionBrush" : "ReceiveDirectionBrush";
            return Application.Current.TryFindResource(key) as Brush;
        }
        return Application.Current.TryFindResource("PrimaryTextBrush") as Brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
