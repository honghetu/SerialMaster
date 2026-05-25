using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SerialMaster.UI.Converters;

public class BytesToAsciiConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not byte[] data || data.Length == 0) return "";

        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (b >= 0x20 && b < 0x7F)
                sb.Append((char)b);
            else if (b == 0x0D)
                sb.Append("");
            else if (b == 0x0A)
                sb.Append('\n');
            else if (b == 0x09)
                sb.Append('\t');
            else
                sb.Append('.');
        }
        return sb.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
