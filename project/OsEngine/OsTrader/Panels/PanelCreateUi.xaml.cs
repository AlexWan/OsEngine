/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels
{
    public partial class PanelCreateUi
    {
        public PanelCreateUi()
        {
            InitializeComponent();

            ComboBoxStrategyType.ItemsSource = PanelCreator.GetNamesStrategy();
            ComboBoxStrategyType.SelectedIndex = 0;
            TextBoxName.Text = "MyNewBot";

            Title = OsLocalization.Trader.Label59;
            LabelStrategyType.Content = OsLocalization.Trader.Label60;
            LabelName.Content = OsLocalization.Trader.Label61;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

        }

        public bool IsAccepted;

        public string NameBot;

        public string NameStrategy;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(TextBoxName.Text))
            {
                MessageBox.Show(OsLocalization.Trader.Label58);
                return;
            }

            NameStrategy = ComboBoxStrategyType.Text;
            NameBot = TextBoxName.Text;
            IsAccepted = true;
            Close();
        }
    }
}
