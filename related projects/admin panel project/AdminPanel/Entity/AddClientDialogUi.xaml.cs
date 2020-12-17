using AdminPanel.Language;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AdminPanel.ViewModels;

namespace AdminPanel.Entity
{
    /// <summary>
    /// Логика взаимодействия для AddClientDialogUi.xaml
    /// </summary>
    public partial class AddClientDialogUi : Window
    {
        public string CreatedName;

        private readonly List<ClientViewModel> _allClientsName;

        public AddClientDialogUi(List<ClientViewModel> allClientsName)
        {
            InitializeComponent();
            _allClientsName = allClientsName;
            Title = OsLocalization.MainWindow.AddClientTitle;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            var name = ClientName.Text;

            if (_allClientsName == null || _allClientsName.Find(c => c.Name == name) == null)
            {
                CreatedName = ClientName.Text;
                Close();
                return;
            }

            var message = OsLocalization.MainWindow.NewClientErrorName;
            TbText.Text = message;

            Task.Run(() =>
            {
                Thread.Sleep(5000);
                Dispatcher.Invoke(() => TbText.Text = null);
            });
        }
    }
}
