using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VliveSubsNotification.Converters
{
    public class TimespanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ts = (TimeSpan)value;
            return (int)ts.TotalHours > 0 ? $"{Math.Floor(ts.TotalHours):0}:{ts.Minutes:00} min" : $"{Math.Max(1, ts.Minutes)} min";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}