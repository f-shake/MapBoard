using MapBoard.Query;
using System;
using System.Globalization;

namespace MapBoard.Converters
{
    public class SqlValueTypeToVisibilityConverter : IValueConverter
    {
        public static SqlValueTypeToVisibilityConverter Instance = new SqlValueTypeToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SqlWhereClauseItemValueType type && parameter is string typeName)
            {
                return type.ToString() == typeName;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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