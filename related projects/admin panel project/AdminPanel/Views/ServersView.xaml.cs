using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для ServersView.xaml
    /// </summary>
    public partial class ServersView : UserControl
    {
        public ServersView()
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
