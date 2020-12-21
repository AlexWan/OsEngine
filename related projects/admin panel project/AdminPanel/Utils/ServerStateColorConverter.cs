using AdminPanel.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AdminPanel.Utils
{
    public class ServerStateColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is ServerState state)
            {
                return state != ServerState.Connect ?
                    new SolidColorBrush(Color.FromRgb(255, 127, 80))
                    : new SolidColorBrush(Color.FromRgb(0, 128, 0));
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

    public class ServerStateLabelColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is ServerState state)
            {
                return state != ServerState.Connect ?
                    new SolidColorBrush(Color.FromRgb(255, 127, 80))
                    : new SolidColorBrush(Color.FromRgb(177, 197, 27));
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is Status status)
            {
                switch (status)
                {
                    case Status.Ok:
                        return new SolidColorBrush(Color.FromRgb(146, 255, 27));
                    case Status.Error:
                        return new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    case Status.Danger:
                        return new SolidColorBrush(Color.FromRgb(222, 222, 5));
                }
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

    public class AngelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is Visibility mode)
            {
                switch (mode)
                {
                    case Visibility.Collapsed:
                        return 360;

                    case Visibility.Visible:
                        return 180;

                    default:
                        return 180;
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
