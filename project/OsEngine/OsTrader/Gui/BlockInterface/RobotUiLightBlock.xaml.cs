using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System.Windows;
using System.Windows.Input;

namespace OsEngine.OsTrader.Gui.BlockInterface
{
    /// <summary>
    /// Interaction logic for RobotUiLightBlock.xaml
    /// </summary>
    public partial class RobotUiLightBlock : Window
    {
        public RobotUiLightBlock()
        {
            InitializeComponent();
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            string lastPassword = BlockMaster.Password;

            if(string.IsNullOrEmpty(lastPassword) == false)
            {
                for(int i = 0;i < lastPassword.Length;i++)
                {
                    TextBoxPassword.Text += "*";
                    TextBoxPasswordRepeat.Text += "*";
                }

                TextBoxPassword.IsEnabled = false;
                TextBoxPasswordRepeat.IsEnabled = false;
                ImageEye.MouseDown += ImageEye_MouseDown;
                ImageEye.MouseEnter += ImageEye_MouseEnter;
                ImageEye.MouseLeave += ImageEye_MouseLeave;
            }
            else
            {
                ImageEye.Visibility = Visibility.Hidden;
            }

            LabelPassword.Content = OsLocalization.Trader.Label423;
            LabelRepeat.Content = OsLocalization.Trader.Label424;
            ButtonCancel.Content = OsLocalization.Trader.Label425;
            ButtonAccept.Content = OsLocalization.Trader.Label426;
            Title =  OsLocalization.Trader.Label427;
        }

        public bool InterfaceIsBlock;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            string pass1 = TextBoxPassword.Text;
            string pass2 = TextBoxPasswordRepeat.Text;

            if(pass1 != pass2)
            {
                ServerMaster.SendNewLogMessage("Passwords not equal", LogMessageType.Error);
                Close();
                return;
            }

            if(string.IsNullOrEmpty(pass1))
            {
                ServerMaster.SendNewLogMessage("Password is null", LogMessageType.Error);
                Close();
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label428);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            if (TextBoxPassword.IsEnabled == true)
            {
                BlockMaster.Password = pass1;
            }

            BlockMaster.IsBlocked = true;
            InterfaceIsBlock = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ImageEye_MouseLeave(object sender, MouseEventArgs e)
        {
            ImageEye.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void ImageEye_MouseEnter(object sender, MouseEventArgs e)
        {
            ImageEye.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void ImageEye_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ImageEye.Visibility = Visibility.Hidden;
            TextBoxPassword.IsEnabled = true;
            TextBoxPasswordRepeat.IsEnabled = true;

            string lastPassword = BlockMaster.Password;
            TextBoxPassword.Text = lastPassword;
            TextBoxPasswordRepeat.Text = lastPassword;
        }
    }
}