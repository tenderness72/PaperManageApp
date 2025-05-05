using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PaperManagementApp.Converters
{
    /// <summary>
    /// コレクションの数が0より大きい場合にtrueを返すコンバーター
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            // 数値型の場合
            if (value is int intValue)
            {
                return intValue > 0;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}