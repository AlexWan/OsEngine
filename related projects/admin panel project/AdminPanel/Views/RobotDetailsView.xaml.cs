using System.Collections.Generic;
using AdminPanel.Entity;
using System.Windows;
using System.Windows.Controls;
using AdminPanel.ViewModels;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для RobotDetailsView.xaml
    /// </summary>
    public partial class RobotDetailsView : UserControl
    {
        public RobotDetailsView()
        {
            InitializeComponent();
        }

        private void ButtonLog_OnClick(object sender, RoutedEventArgs e)
        {
            var item = GetItem(sender);

            if (item == null)
            {
                return;
            }

            var data = new List<LogMessage>(item.Log);
            
            LogView logUi = new LogView(data);
            logUi.ShowDialog();
        }

        private void ButtonParams_OnClick(object sender, RoutedEventArgs e)
        {
            var item = GetItem(sender);

            if (item == null)
            {
                return;
            }
            ParamsView paramsUi = new ParamsView(item.Params);
            paramsUi.ShowDialog();
        }

        private Robot GetItem(object sender)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return null;
            }
            var item = btn.DataContext as Robot;

            return item;
        }

        private void TextBoxMaxPos_OnTextChanged(object sender, TextChangedEventArgs e)
        {
           ((Robot)DataContext).Save();
        }
        private void TextBoxMaxLot_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ((Robot)DataContext).Save();
        }
    }
}
