using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SerialMaster.UI.Converters;

/// <summary>
/// Visible if value.ToString() == parameter.ToString(), else Collapsed.
/// Used to switch between multiple data view templates (HEX/ASCII/Dual/Terminal).
/// </summary>
public class DisplayModeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
