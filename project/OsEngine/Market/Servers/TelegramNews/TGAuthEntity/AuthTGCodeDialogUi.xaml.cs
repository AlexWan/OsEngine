using OsEngine.Language;
using System.Windows;

namespace OsEngine.Market.Servers.TelegramNews.TGAuthEntity
{
    /// <summary>
    /// Логика взаимодействия для AuthTGCodeDialogUi.xaml
    /// </summary>
    public partial class AuthTGCodeDialogUi : Window
    {
        public string VerificationCode { get; private set; }

        public AuthTGCodeDialogUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.Market.TelegramAuthTitle;
            LabelCode.Content = OsLocalization.Market.AuthorizationCode + ":";
            ButtonSend.Content = OsLocalization.Market.SendButton;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

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
