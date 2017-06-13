using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для AcceptDialogUi.xaml
    /// </summary>
    public partial class AcceptDialogUi : Window
    {

        /// <summary>
        /// Пользователь одобрил проводитмое действие
        /// </summary>
        public bool UserAcceptActioin;

        public AcceptDialogUi(string text)
        {
            InitializeComponent();
            LabelText.Content = text;
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
