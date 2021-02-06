/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;

namespace OsEngine.Charts.CandleChart.Indicators
{

    /// <summary>
    /// Interaction logic  for IndicarotCreateUi.xaml
    /// Логика взаимодействия для IndicarotCreateUi.xaml
    /// </summary>
    public partial class IndicarotCreateUi
    {

        /// <summary>
        /// indicator drawing element
        /// элемент для прорисовки индикаторов
        /// </summary>
        private DataGridView _gridViewIndicators;

        /// <summary>
        /// data area drawing element
        /// элемент для прорисовки областей данных
        /// </summary>
        private DataGridView _gridViewAreas;

        /// <summary>
        /// class indicator manager
        /// класс менеджер индикаторов
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="chartMaster">class indicator manager/класс менеджер индикаторов</param>
        public IndicarotCreateUi(ChartCandleMaster chartMaster)
        {
            InitializeComponent();
            _chartMaster = chartMaster;

            _gridViewIndicators = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            _gridViewIndicators.ReadOnly = true;
            _gridViewIndicators.ScrollBars = ScrollBars.Vertical;
            HostNames.Child = _gridViewIndicators;
            DataGridViewColumn column = new DataGridViewColumn();
            column.HeaderText = OsLocalization.Charts.LabelIndicatorType;
            column.CellTemplate = new DataGridViewTextBoxCell();
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridViewIndicators.Columns.Add(column);

            _gridViewIndicators.Rows.Add("Adaptive Look Back");
            _gridViewIndicators.Rows.Add("ADX");
            _gridViewIndicators.Rows.Add("ATR");
            _gridViewIndicators.Rows.Add("Alligator");
            _gridViewIndicators.Rows.Add("AO");
            _gridViewIndicators.Rows.Add("AC");
            _gridViewIndicators.Rows.Add("AccumulationDistribution");
            _gridViewIndicators.Rows.Add("Bollinger");
            _gridViewIndicators.Rows.Add("BFMFI");
            _gridViewIndicators.Rows.Add("BullsPower");
            _gridViewIndicators.Rows.Add("BearsPower");
            _gridViewIndicators.Rows.Add("CMO");
            _gridViewIndicators.Rows.Add("CCI");
            _gridViewIndicators.Rows.Add("DonchianChannel");
            _gridViewIndicators.Rows.Add("Envelops");
            _gridViewIndicators.Rows.Add("Efficiency Ratio");
            _gridViewIndicators.Rows.Add("Fractal");
            _gridViewIndicators.Rows.Add("Force Index");
            _gridViewIndicators.Rows.Add("OnBalanceVolume");
            _gridViewIndicators.Rows.Add("Ichimoku");
            _gridViewIndicators.Rows.Add("IvashovRange");
            _gridViewIndicators.Rows.Add("KalmanFilter");
            _gridViewIndicators.Rows.Add("LinearRegressionCurve");
            _gridViewIndicators.Rows.Add("Moving Average");
            _gridViewIndicators.Rows.Add("MACD Histogram");
            _gridViewIndicators.Rows.Add("MACD Line");
            _gridViewIndicators.Rows.Add("Momentum");
            _gridViewIndicators.Rows.Add("MoneyFlowIndex");
            _gridViewIndicators.Rows.Add("Parabolic SAR");
            _gridViewIndicators.Rows.Add("Price Channel");
            _gridViewIndicators.Rows.Add("Price Oscillator");
            _gridViewIndicators.Rows.Add("Pivot");
            _gridViewIndicators.Rows.Add("Pivot Points");
            _gridViewIndicators.Rows.Add("RSI");
            _gridViewIndicators.Rows.Add("ROC");
            _gridViewIndicators.Rows.Add("RVI");
            _gridViewIndicators.Rows.Add("SimpleVWAP");
            _gridViewIndicators.Rows.Add("Standard Deviation");
            _gridViewIndicators.Rows.Add("Stochastic Oscillator");
            _gridViewIndicators.Rows.Add("Stochastic Rsi");
            _gridViewIndicators.Rows.Add("TickVolume");
            _gridViewIndicators.Rows.Add("Trix");
            _gridViewIndicators.Rows.Add("TradeThread");
            _gridViewIndicators.Rows.Add("Unk");
            _gridViewIndicators.Rows.Add("UltimateOscillator");
            _gridViewIndicators.Rows.Add("VerticalHorizontalFilter");
            _gridViewIndicators.Rows.Add("Volume Oscillator");
            _gridViewIndicators.Rows.Add("Volume");
            _gridViewIndicators.Rows.Add("VWAP");
            _gridViewIndicators.Rows.Add("WilliamsRange");

            _gridViewIndicators.Click += delegate { _lastScriptGrid = false; };

            _gridViewAreas = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            HostArea.Child = _gridViewAreas;

            _gridViewAreas.ReadOnly = true;


            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.HeaderText = OsLocalization.Charts.LabelIndicatorAreasOnChart;
            column1.CellTemplate = new DataGridViewTextBoxCell();
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridViewAreas.Columns.Add(column1);

            List<string> areas = chartMaster.GetChartAreas();

            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] != "TradeArea")
                {
                    _gridViewAreas.Rows.Add(areas[i]);
                }
            }

            _gridViewAreas.Rows.Add("NewArea");

            Title = OsLocalization.Charts.TitleIndicatorCreateUi;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

            ItemIncluded.Header = OsLocalization.Charts.Label6;
            TabItemScript.Header = OsLocalization.Charts.Label7;

            CreateGridScriptIndicators();

            TabControlIndicatorsNames.SelectionChanged += delegate
            {
                if (TabControlIndicatorsNames.SelectedIndex == 0)
                {
                    _lastScriptGrid = false;
                }
                else if (TabControlIndicatorsNames.SelectedIndex == 1)
                {
                    _lastScriptGrid = true;
                }
            };
        }

        public IIndicator IndicatorCandle;

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScriptGrid)
            {
                AcceptCreationScriptIndicator();
                return;
            }

            string areaName = _gridViewAreas.SelectedCells[0].Value.ToString();

            if (areaName == "NewArea")
            {
                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.AreaIsCreate("NewArea" + i) == false)
                    {
                        areaName = "NewArea" + i;
                        break;
                    }
                }
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Trades")
            {
                _chartMaster.CreateTickChart();
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "TickVolume")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "TickVolume" + i) == false)
                    {
                        name = "VWAP" + i;
                        break;
                    }
                }
                IndicatorCandle = new TickVolume(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "VWAP")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "VWAP" + i) == false)
                    {
                        name = "VWAP" + i;
                        break;
                    }
                }
                IndicatorCandle = new Vwap(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "UltimateOscillator")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "UltimateOscillator" + i) == false)
                    {
                        name = "UltimateOscillator" + i;
                        break;
                    }
                }
                IndicatorCandle = new UltimateOscillator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "KalmanFilter")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "KalmanFilter" + i) == false)
                    {
                        name = "KalmanFilter" + i;
                        break;
                    }
                }
                IndicatorCandle = new KalmanFilter(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Moving Average")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Sma" + i) == false)
                    {
                        name = "Sma" + i;
                        break;
                    }
                }
                IndicatorCandle = new MovingAverage(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Volume")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Volume" + i) == false)
                    {
                        name = "Volume" + i;
                        break;
                    }
                }

                IndicatorCandle = new Volume(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);

            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Price Channel")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Price Channel" + i) == false)
                    {
                        name = "Price Channel" + i;
                        break;
                    }
                }
                IndicatorCandle = new PriceChannel(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Bollinger")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Bollinger" + i) == false)
                    {
                        name = "Bollinger" + i;
                        break;
                    }
                }
                IndicatorCandle = new Bollinger(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "DonchianChannel")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "DonchianChannel" + i) == false)
                    {
                        name = "DonchianChannel" + i;
                        break;
                    }
                }
                IndicatorCandle = new DonchianChannel(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BFMFI")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "BFMFI" + i) == false)
                    {
                        name = "BFMFI" + i;
                        break;
                    }
                }
                IndicatorCandle = new BfMfi(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BullsPower")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "BullsPower" + i) == false)
                    {
                        name = "BullsPower" + i;
                        break;
                    }
                }
                IndicatorCandle = new BullsPower(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "CMO")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "CMO" + i) == false)
                    {
                        name = "CMO" + i;
                        break;
                    }
                }
                IndicatorCandle = new Cmo(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BearsPower")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "BearsPower" + i) == false)
                    {
                        name = "BeasPower" + i;
                        break;
                    }
                }
                IndicatorCandle = new BearsPower(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Stochastic Oscillator")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "StochasticOscillator" + i) == false)
                    {
                        name = "StochasticOscillator" + i;
                        break;
                    }
                }
                IndicatorCandle = new StochasticOscillator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Stochastic Rsi")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "StochasticRsi" + i) == false)
                    {
                        name = "StochasticRsi" + i;
                        break;
                    }
                }
                IndicatorCandle = new StochRsi(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "RSI")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "RSI" + i) == false)
                    {
                        name = "RSI" + i;
                        break;
                    }
                }
                IndicatorCandle = new Rsi(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ROC")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "ROC" + i) == false)
                    {
                        name = "ROC" + i;
                        break;
                    }
                }
                IndicatorCandle = new Roc(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "RVI")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "RVI" + i) == false)
                    {
                        name = "RVI" + i;
                        break;
                    }
                }
                IndicatorCandle = new Rvi(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Alligator")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Alligator" + i) == false)
                    {
                        name = "Alligator" + i;
                        break;
                    }
                }
                IndicatorCandle = new Alligator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AO")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "AO" + i) == false)
                    {
                        name = "AO" + i;
                        break;
                    }
                }
                IndicatorCandle = new AwesomeOscillator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AccumulationDistribution")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "AccumulationDistribution" + i) == false)
                    {
                        name = "AccumulationDistribution" + i;
                        break;
                    }
                }
                IndicatorCandle = new AccumulationDistribution(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Force Index")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Force Index" + i) == false)
                    {
                        name = "Force Index" + i;
                        break;
                    }
                }
                IndicatorCandle = new ForceIndex(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "OnBalanceVolume")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "OnBalanceVolume" + i) == false)
                    {
                        name = "OnBalanceVolume" + i;
                        break;
                    }
                }
                IndicatorCandle = new OnBalanceVolume(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Fractal")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Fractal" + i) == false)
                    {
                        name = "Fractal" + i;
                        break;
                    }
                }
                IndicatorCandle = new Fractal(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ADX")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "ADX" + i) == false)
                    {
                        name = "ADX" + i;
                        break;
                    }
                }
                IndicatorCandle = new Adx(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ATR")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "ATR" + i) == false)
                    {
                        name = "ATR" + i;
                        break;
                    }
                }
                IndicatorCandle = new Atr(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Price Oscillator")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Price Oscillator" + i) == false)
                    {
                        name = "Price Oscillator" + i;
                        break;
                    }
                }
                IndicatorCandle = new PriceOscillator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MACD Histogram")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "MACD Histogram" + i) == false)
                    {
                        name = "MACD Histogram" + i;
                        break;
                    }
                }
                IndicatorCandle = new MacdHistogram(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MACD Line")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "MACD Line" + i) == false)
                    {
                        name = "MACD Line" + i;
                        break;
                    }
                }
                IndicatorCandle = new MacdLine(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Momentum")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Momentum" + i) == false)
                    {
                        name = "Momentum" + i;
                        break;
                    }
                }
                IndicatorCandle = new Momentum(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MoneyFlowIndex")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "MoneyFlowIndex" + i) == false)
                    {
                        name = "MoneyFlowIndex" + i;
                        break;
                    }
                }
                IndicatorCandle = new MoneyFlowIndex(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Envelops")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Envelops" + i) == false)
                    {
                        name = "Envelops" + i;
                        break;
                    }
                }
                IndicatorCandle = new Envelops(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Efficiency Ratio")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "EfficiencyRatio" + i) == false)
                    {
                        name = "EfficiencyRatio" + i;
                        break;
                    }
                }
                IndicatorCandle = new EfficiencyRatio(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Adaptive Look Back")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Adaptive Look Back" + i) == false)
                    {
                        name = "Adaptive Look Back" + i;
                        break;
                    }
                }
                IndicatorCandle = new AdaptiveLookBack(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "IvashovRange")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "IvashovRange" + i) == false)
                    {
                        name = "IvashovRange" + i;
                        break;
                    }
                }
                IndicatorCandle = new IvashovRange(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Volume Oscillator")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Volume Oscillator" + i) == false)
                    {
                        name = "Volume Oscillator" + i;
                        break;
                    }
                }
                IndicatorCandle = new VolumeOscillator(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Parabolic SAR")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Parabolic SAR" + i) == false)
                    {
                        name = "Parabolic SAR" + i;
                        break;
                    }
                }
                IndicatorCandle = new ParabolicSaR(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "CCI")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "CCI" + i) == false)
                    {
                        name = "CCI" + i;
                        break;
                    }
                }
                IndicatorCandle = new Cci(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }


            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Standard Deviation")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Standard Deviation" + i) == false)
                    {
                        name = "Standard Deviation" + i;
                        break;
                    }
                }
                IndicatorCandle = new StandardDeviation(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AC")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "AC" + i) == false)
                    {
                        name = "AC" + i;
                        break;
                    }
                }
                IndicatorCandle = new Ac(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "VerticalHorizontalFilter")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "VerticalHorizontalFilter" + i) == false)
                    {
                        name = "VerticalHorizontalFilter" + i;
                        break;
                    }
                }
                IndicatorCandle = new VerticalHorizontalFilter(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "WilliamsRange")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "WilliamsRange" + i) == false)
                    {
                        name = "WilliamsRange" + i;
                        break;
                    }
                }
                IndicatorCandle = new WilliamsRange(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Trix")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Trix" + i) == false)
                    {
                        name = "Trix" + i;
                        break;
                    }
                }
                IndicatorCandle = new Trix(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Ichimoku")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Ichimoku" + i) == false)
                    {
                        name = "Ichimoku" + i;
                        break;
                    }
                }
                IndicatorCandle = new Ichimoku(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "TradeThread")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "TradeThread" + i) == false)
                    {
                        name = "TradeThread" + i;
                        break;
                    }
                }
                IndicatorCandle = new TradeThread(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Pivot")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Pivot" + i) == false)
                    {
                        name = "Pivot" + i;
                        break;
                    }
                }
                IndicatorCandle = new Pivot(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Pivot Points")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "PivotPoints" + i) == false)
                    {
                        name = "PivotPoints" + i;
                        break;
                    }
                }
                IndicatorCandle = new PivotPoints(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "LinearRegressionCurve")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "LinearRegressionCurve" + i) == false)
                    {
                        name = "LinearRegressionCurve" + i;
                        break;
                    }
                }
                IndicatorCandle = new LinearRegressionCurve(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "SimpleVWAP")
            {
                string name = "";

                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "SimpleVWAP" + i) == false)
                    {
                        name = "SimpleVWAP" + i;
                        break;
                    }
                }
                IndicatorCandle = new SimpleVWAP(_chartMaster.Name + name, true);
                _chartMaster.CreateIndicator(IndicatorCandle, areaName);
            }


            Close();

            if (IndicatorCandle != null)
            {
                IndicatorCandle.ShowDialog();
            }

        }

        // script

        private DataGridView _gridNamesScript;

        private bool _lastScriptGrid;

        private void CreateGridScriptIndicators()
        {
            _gridNamesScript = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            _gridNamesScript.ReadOnly = true;
            _gridNamesScript.ScrollBars = ScrollBars.Vertical;
            HostNamesScript.Child = _gridNamesScript;
            DataGridViewColumn column = new DataGridViewColumn();
            column.HeaderText = OsLocalization.Charts.LabelIndicatorType;
            column.CellTemplate = new DataGridViewTextBoxCell();
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridNamesScript.Columns.Add(column);

            List<string> indName = IndicatorsFactory.GetIndicatorsNames();

            for (int i = 0; i < indName.Count; i++)
            {
                _gridNamesScript.Rows.Add(indName[i]);
            }
            _gridNamesScript.Click += delegate { _lastScriptGrid = true; };
        }

        private void AcceptCreationScriptIndicator()
        {
            string areaName = _gridViewAreas.SelectedCells[0].Value.ToString();

            if (areaName == "NewArea")
            {
                for (int i = 0; i < 30; i++)
                {
                    if (_chartMaster.AreaIsCreate("NewArea" + i) == false)
                    {
                        areaName = "NewArea" + i;
                        break;
                    }
                }
            }

            string indicatorName = _gridNamesScript.SelectedCells[0].Value.ToString();

            string name = "";

            for (int i = 0; i < 30; i++)
            {
                if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + indicatorName + i) == false)
                {
                    name = indicatorName + i;
                    break;
                }
            }

            IndicatorCandle = IndicatorsFactory.CreateIndicatorByName(indicatorName, _chartMaster.Name + name, true);
            _chartMaster.CreateIndicator(IndicatorCandle, areaName);

            Close();

            if (IndicatorCandle != null)
            {
                IndicatorCandle.ShowDialog();
            }
        }
    }
}
