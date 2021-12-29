using AdminPanel.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для ClientPreview.xaml
    /// </summary>
    public partial class ClientPreview : UserControl
    {
        public ClientPreview()
        {
            InitializeComponent();
        }

        private void GoToSlaveClick(object sender, RoutedEventArgs e)
        {
            GoTo(0, sender);
        }
        private void GoToServersClick(object sender, RoutedEventArgs e)
        {
            GoTo(1, sender);
        }
        private void GoToRobotsClick(object sender, RoutedEventArgs e)
        {
            GoTo(2, sender);
        }
        private void GoToPositionsClick(object sender, RoutedEventArgs e)
        {
            GoTo(3, sender);
        }
        private void GoToPortfoliosClick(object sender, RoutedEventArgs e)
        {
            GoTo(4, sender);
        }
        private void GoToOrdersClick(object sender, RoutedEventArgs e)
        {
            GoTo(5, sender);
        }

        private void GoTo(int index, object sender)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }

            if (btn.DataContext is ClientViewModel item)
            {
                item.SetActiveTab(index);
            }
        }
    }
}

