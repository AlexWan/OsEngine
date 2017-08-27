using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Charts;
using OsEngine.Charts.CandleChart;

namespace OsEngine.OsTrader.Panels
{
    /// <summary>
    /// Логика взаимодействия для BotPanelChartUI.xaml
    /// </summary>
    public partial class BotPanelChartUi
    {
        public BotPanelChartUi(BotPanel panel)
        {
            InitializeComponent();
            _panel = panel;
            CreateTabs();
            TabControlBotsName.SelectionChanged += TabControlBotsName_SelectionChanged;
            _chart = new ChartPainter(panel.NameStrategyUniq);
            PaintActivTab(0);
        }

        private BotPanel _panel;

        private ChartPainter _chart;

        private void CreateTabs()
        {
            TabControlBotsName.Items.Clear();
            for (int i = 0; i < _panel.TabsSimple.Count; i++)
            {
                TabControlBotsName.Items.Add(_panel.TabsSimple[i].Securiti.Name.Replace(".txt","") + _panel.TabsSimple[i].TimeFrame);
            }

            for (int i = 0; i < _panel.TabsIndex.Count; i++)
            {
                TabControlBotsName.Items.Add("Index " + (i+1));
            }
        }

        void TabControlBotsName_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
           PaintActivTab(TabControlBotsName.SelectedIndex);
        }

        private int _lastTabNum = -1;


        private void PaintActivTab(int tabNum)
        {
            if (_lastTabNum == tabNum)
            {
                return;
            }
            _lastTabNum = tabNum;

            _chart.StopPaint();

            _chart.Clear();

            if (tabNum < _panel.TabsSimple.Count)
            {
                _chart.PaintCandles(_panel.TabsSimple[tabNum].CandlesFinishedOnly);
                _chart.StartPaint(ChartHostPanel, RectChart);
                _chart.PaintPositions(_panel.TabsSimple[tabNum].PositionsAll);

                for (int i = 0;_panel.TabsSimple[tabNum].Indicators != null && i < _panel.TabsSimple[tabNum].Indicators.Count; i++)
                {
                    ChartArea area = _chart.CreateArea("Area" + _panel.TabsSimple[tabNum].Indicators[i].Name, 15);
                    _panel.TabsSimple[tabNum].Indicators[i].NameSeries = _chart.CreateSeries(area,
                        _panel.TabsSimple[tabNum].Indicators[i].TypeIndicator, _panel.TabsSimple[tabNum].Indicators[i].NameSeries);

                    _chart.PaintIndicator(_panel.TabsSimple[tabNum].Indicators[i]);
                }
            }
            else
            {
                tabNum = tabNum - _panel.TabsSimple.Count;

                _chart.PaintCandles(_panel.TabsIndex[tabNum].Candles);
                _chart.StartPaint(ChartHostPanel, RectChart);

                for (int i = 0; _panel.TabsIndex[tabNum].Indicators !=  null && i < _panel.TabsIndex[tabNum].Indicators.Count; i++)
                {
                    ChartArea area = _chart.CreateArea("Area" + _panel.TabsIndex[tabNum].Indicators[i].Name, 15);
                    _panel.TabsIndex[tabNum].Indicators[i].NameSeries = _chart.CreateSeries(area,
                        _panel.TabsIndex[tabNum].Indicators[i].TypeIndicator, _panel.TabsIndex[tabNum].Indicators[i].Name + i);

                    _chart.PaintIndicator(_panel.TabsIndex[tabNum].Indicators[i]);
                }
            }

            
        }
    }
}
