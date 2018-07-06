using System.Windows;
using OsEngine.Charts.CandleChart;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для BotWhithTwoTimeFrameUi.xaml
    /// </summary>
    public partial class BotWhithTwoTimeFrameUi : Window
    {
        private ChartPainter _painter;

        private BotWhithTwoTimeFrame _bot;

        public BotWhithTwoTimeFrameUi(BotWhithTwoTimeFrame bot)
        {
            InitializeComponent();
            _painter = new ChartPainter("chart");
            _painter.StartPaintPrimeChart(HostChart,Rectangle);
            _bot = bot;
        }

        private void ButtonPaint_Click(object sender, RoutedEventArgs e)
        {
            _painter.ClearDataPointsAndSizeValue();
            if (_bot.MergeCandles != null && _bot.MergeCandles.Count != 0)
            {
                _painter.ProcessCandles(_bot.MergeCandles);
            }
        }
    }
}
