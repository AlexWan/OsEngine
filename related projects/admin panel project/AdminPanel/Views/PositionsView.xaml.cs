using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для PositionsView.xaml
    /// </summary>
    public partial class PositionsView : UserControl
    {
        public PositionsView()
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
