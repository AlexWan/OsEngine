using AdminPanel.Entity;
using System.Windows;
using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для RobotsView.xaml
    /// </summary>
    public partial class RobotsView : UserControl
    {
        public RobotsView()
        {
            InitializeComponent();
        }
        
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }
            var item = btn.DataContext as Robot;
            var index = RobotsGrid.Items.IndexOf(item);

            DataGridRow selectedRow = RobotsGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;

            if (selectedRow.DetailsVisibility == Visibility.Collapsed)
            {
                selectedRow.DetailsVisibility = Visibility.Visible;
            }
            else
            {
                selectedRow.DetailsVisibility = Visibility.Collapsed;
            }
        }
    }
}
