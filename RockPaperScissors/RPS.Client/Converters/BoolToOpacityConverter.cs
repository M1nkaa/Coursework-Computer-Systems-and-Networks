using System;
using System.Globalization;
using System.Windows.Data;

namespace RPS.Client.Converters
{
    // Конвертер для привязки в XAML: true → 0.5 (затемнённо), false → 1.0 (нормально).
    // Используется чтобы визуально блокировать кнопки после сделанного хода.
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? 0.5 : 1.0;
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
