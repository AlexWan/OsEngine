using System.Windows;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для ProxyAddUi.xaml
    /// </summary>
    public partial class ProxyHolderAddUi
    {
        public ProxyHolderAddUi()
        {
            InitializeComponent();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(TextBoxIP.Text) ||
                string.IsNullOrWhiteSpace(TextBoxName.Text) ||
                string.IsNullOrWhiteSpace(TextBoxPassword.Text))
            {
                MessageBox.Show("Не все данные заполнены");
                return;
            }

            Proxy = new ProxyHolder();

            Proxy.Ip = TextBoxIP.Text;
            Proxy.UserName = TextBoxName.Text;
            Proxy.UserPassword = TextBoxPassword.Text;

            Close();
        }

        public ProxyHolder Proxy;
    }
}
