using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RelatorioGLPIApp.Converters
{
    public class SortDirectionToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isAscending)
            {
                return isAscending ? "M7 14l5-5 5 5H7z" : "M7 10l5 5 5-5H7z"; // Seta para cima : Seta para baixo
            }
            return "M7 10l5 5 5-5H7z"; // Padrão: Seta para baixo
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}