using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Логика взаимодействия для BotTabClusterUi.xaml
    /// </summary>
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
                _lineStep = Convert.ToDecimal(TextBoxStep.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
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
