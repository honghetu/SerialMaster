using System.Globalization;
using System.Windows.Data;
using SerialMaster.Core.Models;

namespace SerialMaster.UI.Converters;

public class StatusToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RecordStatus status)
        {
            return status switch
            {
                RecordStatus.Success => "●",
                RecordStatus.Failed => "🔴",
                RecordStatus.Timeout => "⚠",
                _ => "●"
            };
        }
        return "●";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
