using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsMiner
{
    /// <summary>
    /// Interaction Logic for OsMinerSetUi.xaml
    /// Логика взаимодействия для OsMinerSetUi.xaml
    /// </summary>
    public partial class OsMinerSetUi
    {
        public OsMinerSetUi(int numSet, OsMinerSet set)
        {
            InitializeComponent();
            _set = set;

            if (string.IsNullOrEmpty(_set.Name))
            {
                TextBoxSetName.Text = OsLocalization.Miner.Label1 + (numSet);
            }
            else
            {
                TextBoxSetName.Text = set.Name;
            }
            TextBoxSetName.Focus();

            Title = OsLocalization.Miner.Title1;
            Label3.Content = OsLocalization.Miner.Label3;
            ButtonAccept.Content = OsLocalization.Miner.Button1;
        }

        public bool IsActivate;

        private OsMinerSet _set;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxSetName.Text))
            {
                MessageBox.Show(OsLocalization.Miner.Label2);
                return;
            }
            IsActivate = true;
            _set.Name = TextBoxSetName.Text;
            Close();
        }
    }
}
