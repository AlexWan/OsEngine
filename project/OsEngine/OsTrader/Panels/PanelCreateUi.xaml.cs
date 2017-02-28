/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;

namespace OsEngine.OsTrader.Panels
{
    /// <summary>
    /// Логика взаимодействия для StrategOneSecurityCreateUi.xaml
    /// </summary>
    public partial class PanelCreateUi
    {
        public PanelCreateUi()
        {
            InitializeComponent();

            ComboBoxStrategyType.ItemsSource = PanelCreator.GetNamesStrategy();
            ComboBoxStrategyType.SelectedIndex = 0;

            TextBoxName.Text = "MyNewBot";
        }

        public bool IsAccepted;

        public string NameBot;

        public string NameStrategy;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(TextBoxName.Text))
            {
                MessageBox.Show("Не верное имя. Не возможно продолжить процесс создания бота.");
                return;
            }

            NameStrategy = ComboBoxStrategyType.Text;
            NameBot = TextBoxName.Text;
            IsAccepted = true;
            Close();
        }
    }
}
