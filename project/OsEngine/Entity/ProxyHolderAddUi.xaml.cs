/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.Entity
{
    /// <summary>
    /// Interaction Logic for ProxyAddUi.xaml 
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
                MessageBox.Show(OsLocalization.Entity.ErrorSave);
                return;
            }

            Proxy = new ProxyHolder();

            Proxy.Ip = TextBoxIP.Text;
            Proxy.UserName = TextBoxName.Text;
            Proxy.UserPassword = TextBoxPassword.Text;

            Close();
            ButtonSave.Content = OsLocalization.Entity.ProxiesLabel3;
            Title = OsLocalization.Entity.TitleProxyAddUi;
        }

        public ProxyHolder Proxy;
    }
}
