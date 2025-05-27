using System.Windows;


namespace OsEngine.Market.Servers.TelegramNews.TGAuthEntity
{
    /// <summary>
    /// Логика взаимодействия для AuthTGPasswordDialogUi.xaml
    /// </summary>
    public partial class AuthTGPasswordDialogUi : Window
    {
        public string Password { get; private set; }

        public AuthTGPasswordDialogUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            this.Activate();
            this.Focus();
        }

        private void ButtonSendPass_Click(object sender, RoutedEventArgs e)
        {
            Password = TextBoxCode.Text;
            DialogResult = true;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
