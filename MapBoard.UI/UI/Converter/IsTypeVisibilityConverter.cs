using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MapBoard.UI.Converter
{
    public class IsTypeVisibilityConverter : IValueConverter
    {
        public static IsTypeVisibilityConverter Instance { get; } = new IsTypeVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            // 处理Type类型参数
            if (parameter is Type type)
            {
                return type.IsInstanceOfType(value) ? Visibility.Visible : Visibility.Collapsed;
            }

            // 处理字符串类型参数（如"System.String"）
            if (parameter is string typeName)
            {
                var actualType = value.GetType();
                return actualType.FullName == typeName || actualType.Name == typeName
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}