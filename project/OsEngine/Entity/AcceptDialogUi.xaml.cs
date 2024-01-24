using System.Windows;
using OsEngine.Language;


namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction logic for AcceptDialogUi.xaml
    /// </summary>
    public partial class AcceptDialogUi
    {

        /// <summary>
        /// The user has approved the action to be taken
        /// </summary>
        public bool UserAcceptActioin;

        /// <summary>
        /// window designer
        /// </summary>
        /// <param name="text">text that will be displayed as the main message to the user. What he will have to approve/текст который будет выведен в качестве основного сообщения пользователю. То что он должен будет одобрить</param>
        public AcceptDialogUi(string text)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            TextBoxMessage.Text = text;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

            Title = OsLocalization.Entity.TitleAcceptDialog;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

            this.Activate();
            this.Focus();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            UserAcceptActioin = false;
            Close();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            UserAcceptActioin = true;
            Close();
        }
    }
}
