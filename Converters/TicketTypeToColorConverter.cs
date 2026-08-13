using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RelatorioGLPIApp.Converters;

public class TicketTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string type)
        {
            return type switch
            {
                "Plantão" => Brushes.Orange,
                "Normal" => Brushes.DodgerBlue,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}