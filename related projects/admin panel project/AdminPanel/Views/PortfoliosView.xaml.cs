using System.Windows.Controls;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для PortfoliosView.xaml
    /// </summary>
    public partial class PortfoliosView : UserControl
    {
        public PortfoliosView()
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
