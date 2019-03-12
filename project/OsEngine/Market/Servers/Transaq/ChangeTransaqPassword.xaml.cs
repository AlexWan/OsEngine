using System.Windows;
using OsEngine.Language;

namespace OsEngine.Market.Servers.Transaq
{
    /// <summary>
    /// Interaction logic for ChangeTransaqPassword.xaml
    /// Логика взаимодействия для ChangeTransaqPassword.xaml
    /// </summary>
    public partial class ChangeTransaqPassword : Window
    {
        private TransaqServerRealization _server;

        public ChangeTransaqPassword(string message, TransaqServerRealization server)
        {
            InitializeComponent();
            _server = server;
            TextInfo.Text = message;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewPassword.Text) || NewPassword.Text.Length > 19)
            {
                MessageBox.Show(OsLocalization.Market.Message96);
            }
            else
            {
                _server.ChangePassword(OldPassword.Text, NewPassword.Text);
                Close();
            }            
        }
    }
}
