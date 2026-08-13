using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RelatorioGLPIApp.Converters
{
    public class BooleanToChevronConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && isExpanded)
            {
                // Ícone de seta para baixo
                return "M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z";
            }
            // Ícone de seta para a direita
            return "M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}