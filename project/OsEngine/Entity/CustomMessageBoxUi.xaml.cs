using OsEngine.Language;
using System.Windows;

namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction logic for CustomMessageBoxUi.xaml
    /// </summary>
    public partial class CustomMessageBoxUi : Window
    {
        public CustomMessageBoxUi(string text)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            TextBoxMessage.Text = text;

            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;
            Title = OsLocalization.Entity.CustomMessageBoxTitle;

            this.Activate();
            this.Focus();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
