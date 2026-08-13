using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RelatorioGLPIApp.Converters
{
    public class IsPrivateToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPrivate && isPrivate)
            {
                return Brushes.DarkSlateBlue; // Cor para o técnico
            }
            return Brushes.DarkSlateGray; // Cor para o usuário
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}