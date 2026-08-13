using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RelatorioGLPIApp.Converters
{
    public class IsPrivateToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPrivate && isPrivate)
            {
                return new SolidColorBrush(Color.FromRgb(240, 248, 255)); // Fundo para o técnico (AliceBlue)
            }
            return Brushes.White; // Fundo para o usuário
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}