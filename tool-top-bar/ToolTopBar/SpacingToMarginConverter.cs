using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ToolTopBar
{
    /// <summary>
    /// Convierte un valor de spacing (double) a un Thickness para Margin horizontal.
    /// Divide el spacing por 2 para aplicarlo a izquierda y derecha.
    /// </summary>
    public class SpacingToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double spacing)
            {
                var halfSpacing = spacing / 2.0;
                return new Thickness(halfSpacing, 0, halfSpacing, 0);
            }
            return new Thickness(8, 0, 8, 0); // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
