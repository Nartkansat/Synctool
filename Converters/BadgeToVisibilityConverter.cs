using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Synctool.Converters
{
    public class BadgeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;
            if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is string s && int.TryParse(s, out int c)) return c > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
