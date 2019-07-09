/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabClusterUi
    {
        private BotTabCluster _tab;

        public BotTabClusterUi(BotTabCluster tab)
        {
            InitializeComponent();
            _tab = tab;

            TextBoxStep.Text = _tab.LineStep.ToString();
            TextBoxStep.TextChanged += TextBoxStep_TextChanged;
            _lineStep = _tab.LineStep;

            ComboBoxChartType.Items.Add(ClusterType.SummVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.BuyVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.SellVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.DeltaVolume.ToString());
            ComboBoxChartType.SelectedItem = tab.ChartType.ToString();

            Title = OsLocalization.Trader.Label77;
            ButtonConnectorDialog.Content = OsLocalization.Trader.Label78;
            LabelShowType.Content = OsLocalization.Trader.Label79;
            LabelLinesStep.Content = OsLocalization.Trader.Label80;
            ButtonAccept.Content = OsLocalization.Trader.Label17;


        }

        private void TextBoxStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxStep.Text.EndsWith(",") ||
                TextBoxStep.Text.EndsWith("."))
            {
                return;
            }

            try
            {
                _lineStep = TextBoxStep.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxStep.Text = _tab.LineStep.ToString();
            }
        }

        private decimal _lineStep;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _tab.LineStep = _lineStep;

            ClusterType chartType;
            Enum.TryParse(ComboBoxChartType.Text, out chartType);
            _tab.ChartType = chartType;

            Close();
        }

        private void ButtonConnectorDialog_Click(object sender, RoutedEventArgs e)
        {
            _tab.ShowCandlesDialog();
        }
    }
}
