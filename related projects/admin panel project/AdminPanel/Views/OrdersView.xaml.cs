using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для OrdersView.xaml
    /// </summary>
    public partial class OrdersView : UserControl
    {
        public OrdersView()
        {
            InitializeComponent();
        }
        private void DataGrid_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                grid.SelectedItem = null;
            }
        }
    }
}
