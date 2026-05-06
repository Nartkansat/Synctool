using System;
using System.Globalization;
using System.Windows.Data;

namespace ArcelikExcelApp.Converters
{
    public class ParoluTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isParolu)
            {
                return isParolu ? "Parolu" : "Parosuz";
            }
            return "Parosuz";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
