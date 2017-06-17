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
            _painter.StartPaint(HostChart,Rectangle);
            _bot = bot;
        }

        private void ButtonPaint_Click(object sender, RoutedEventArgs e)
        {
            _painter.Clear();
            if (_bot.MergeCandles != null && _bot.MergeCandles.Count != 0)
            {
                _painter.PaintCandles(_bot.MergeCandles);
            }
        }
    }
}
