using System.Windows;

namespace OsEngine.Market.Servers.TelegramNews
{
    /// <summary>
    /// Логика взаимодействия для AuthDialogTGNewsUi.xaml
    /// </summary>
    public partial class AuthTGCodeDialogUi : Window
    {
        public string VerificationCode { get; private set; }

        public AuthTGCodeDialogUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            this.Activate();
            this.Focus();
        }

        private void ButtonSendCode_Click(object sender, RoutedEventArgs e)
        {
            VerificationCode = TextBoxCode.Text;
            DialogResult = true;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
