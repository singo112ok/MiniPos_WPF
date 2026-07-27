using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MiniPos.Converters
{
	internal class PriceToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is decimal price)
			{
				if (price >= 5000)
					return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
				else
					return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748"));
			}

			return Brushes.Black;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo cultre)
		{
			throw new NotImplementedException();
		}
	}
}
