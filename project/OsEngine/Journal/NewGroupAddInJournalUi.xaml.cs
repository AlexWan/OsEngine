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

namespace OsEngine.Journal
{
    /// <summary>
    /// Логика взаимодействия для NewGroupAddInJournalUi.xaml
    /// </summary>
    public partial class NewGroupAddInJournalUi : Window
    {
        public NewGroupAddInJournalUi(List<string> oldGroupNames)
        {
            InitializeComponent();
            _oldGroupNames = oldGroupNames;
        }

        public bool IsAccepted;

        public string NewGroupName;

        private List<string> _oldGroupNames;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            string textInTextBox = TextBoxNewGroupName.Text;

            if(string.IsNullOrEmpty(textInTextBox))
            {
                return;
            }

            for(int i = 0;i < _oldGroupNames.Count;i++)
            {
                if(_oldGroupNames[i].Equals(textInTextBox))
                {
                    return;
                }
            }

            NewGroupName = textInTextBox;
            IsAccepted = true;
            Close();
        }
    }
}
