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
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _server = server;
            TextInfo.Text = message;

            LabelOldPassword.Content = OsLocalization.Market.Label100;
            LabelNewPassword.Content = OsLocalization.Market.Label101;
            ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
            Title = OsLocalization.Market.Label104;

            this.Activate();
            this.Focus();

            Closed += ChangeTransaqPassword_Closed;
        }

        public ChangeTransaqPassword(TransaqServerRealization server)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _server = server;

            LabelOldPassword.Content = OsLocalization.Market.Label100;
            LabelNewPassword.Content = OsLocalization.Market.Label101;
            ButtonAccept.Content = OsLocalization.Market.ButtonAccept;
            Title = OsLocalization.Market.Label104;

            this.Activate();
            this.Focus();

            Closed += ChangeTransaqPassword_Closed;
        }

        private void ChangeTransaqPassword_Closed(object sender, System.EventArgs e)
        {
            _server = null;
            TextInfo = null;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewPassword.Text) || NewPassword.Text.Length > 19)
            {
                MessageBox.Show(OsLocalization.Market.Message96);
            }
            else
            {
                _server.ChangePassword(OldPassword.Text, NewPassword.Text, this);
            }            
        }
    }
}
