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

namespace OsEngine.OsMiner
{
    /// <summary>
    /// Логика взаимодействия для OsMinerSetUi.xaml
    /// </summary>
    public partial class OsMinerSetUi : Window
    {
        public OsMinerSetUi(int numSet, OsMinerSet set)
        {
            InitializeComponent();
            _set = set;

            if (string.IsNullOrEmpty(_set.Name))
            {
                TextBoxSetName.Text = "Набор паттернов №" + (numSet);
            }
            else
            {
                TextBoxSetName.Text = set.Name;
            }
            TextBoxSetName.Focus();
        }

        public bool IsActivate;

        private OsMinerSet _set;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxSetName.Text))
            {
                MessageBox.Show("Имя сета не может быть пустым.");
                return;
            }
            IsActivate = true;
            _set.Name = TextBoxSetName.Text;
            Close();
        }
    }
}
