using AdminPanel.Language;
using AdminPanel.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AdminPanel.Entity
{
    /// <summary>
    /// Логика взаимодействия для LogView.xaml
    /// </summary>
    public partial class LogView : Window
    {
        public LogView(List<LogMessage> messages)
        {
            InitializeComponent();

            Title = OsLocalization.Entity.Log;
            
            DataGridLog.Columns[0].Header = OsLocalization.Entity.TradeColumn4;
            DataGridLog.Columns[1].Header = OsLocalization.Entity.OrderColumn11;
            DataGridLog.Columns[2].Header = OsLocalization.Entity.LogMessage;
            DataGridLog.ItemsSource = messages;
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
