using System;
using System.Globalization;
using System.Windows.Data;

namespace MapBoard.UI.Converter
{
    public class SqlOperatorTypeToItemsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                return Enum.GetValues(enumValue.GetType());
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}