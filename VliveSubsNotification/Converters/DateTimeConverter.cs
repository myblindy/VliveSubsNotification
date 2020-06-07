using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VliveSubsNotification.Converters
{
    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ((DateTime)value).ToString("yyyy-MM-dd HH:mm");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}