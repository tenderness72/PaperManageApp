using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PaperManagementApp.Converters
{
    /// <summary>
    /// bool値をVisibilityに変換するコンバーター
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// bool値をVisibilityに変換（true=Visible, false=Collapsed）
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        /// <summary>
        /// VisibilityをBool値に変換（Visible=true, 他=false）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }

            return false;
        }
    }
}