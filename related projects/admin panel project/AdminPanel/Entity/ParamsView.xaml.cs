using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AdminPanel.Language;

namespace AdminPanel.Entity
{
    /// <summary>
    /// Логика взаимодействия для ParamsView.xaml
    /// </summary>
    public partial class ParamsView : Window
    {
        public ParamsView(Dictionary<string,string> parameters)
        {
            InitializeComponent();

            Title = OsLocalization.Entity.ParametersTitle;

            DataGridLog.Columns[0].Header = OsLocalization.Entity.SecuritiesColumn1;
            DataGridLog.Columns[1].Header = OsLocalization.Entity.Value;
           
            DataGridLog.ItemsSource = parameters;
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
