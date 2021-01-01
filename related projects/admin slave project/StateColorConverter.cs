using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AdminSlave.Model;

namespace AdminSlave
{
    public class StateColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is State state)
            {
                switch (state)
                {
                    case State.Active:
                        return new SolidColorBrush(Color.FromRgb(108, 176, 29));
                    case State.NotAsk:
                        return new SolidColorBrush(Color.FromRgb(215, 27, 33));
                    default:
                        return new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
