/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Charts.CandleChart.Indicators
{

    /// <summary>
    /// Логика взаимодействия для IndicarotCreateUi.xaml
    /// </summary>
    public partial class IndicarotCreateUi
    {

        /// <summary>
        /// элемент для прорисовки индикаторов
        /// </summary>
        private DataGridView _gridViewIndicators;

        /// <summary>
        /// элемент для прорисовки областей данных
        /// </summary>
        private DataGridView _gridViewAreas;

        /// <summary>
        /// класс менеджер индикаторов
        /// </summary>
        private ChartMaster _chartMaster;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="chartMaster">класс менеджер индикаторов</param>
        public IndicarotCreateUi(ChartMaster chartMaster)
        {
            InitializeComponent();
            _chartMaster = chartMaster;

            _gridViewIndicators = new DataGridView();

            HostNames.Child = _gridViewIndicators;

            _gridViewIndicators.AllowUserToOrderColumns = false;
            _gridViewIndicators.AllowUserToDeleteRows = false;
            _gridViewIndicators.AllowUserToAddRows = false;
            _gridViewIndicators.AllowUserToResizeRows = false;
            _gridViewIndicators.RowHeadersVisible = false;
            _gridViewIndicators.ReadOnly = true;
            _gridViewIndicators.AllowUserToResizeColumns = false;

            DataGridViewColumn column = new DataGridViewColumn();
            column.HeaderText = @"Тип индикатора";
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
            _gridViewIndicators.Rows.Add("Envelops");
            _gridViewIndicators.Rows.Add("Efficiency Ratio");
            _gridViewIndicators.Rows.Add("Fractal");
            _gridViewIndicators.Rows.Add("Force Index");
            _gridViewIndicators.Rows.Add("OnBalanceVolume");
            _gridViewIndicators.Rows.Add("Ichimoku");
            _gridViewIndicators.Rows.Add("IvashovRange");
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
            _gridViewIndicators.Rows.Add("StochasticOscillator");
            _gridViewIndicators.Rows.Add("RSI");
            _gridViewIndicators.Rows.Add("ROC");
            _gridViewIndicators.Rows.Add("RVI");
            _gridViewIndicators.Rows.Add("Standard Deviation");
            _gridViewIndicators.Rows.Add("Trix");
            _gridViewIndicators.Rows.Add("TradeThread");
            _gridViewIndicators.Rows.Add("Unk");
            _gridViewIndicators.Rows.Add("VerticalHorizontalFilter");
            _gridViewIndicators.Rows.Add("Volume Oscillator");
            _gridViewIndicators.Rows.Add("Volume");
            _gridViewIndicators.Rows.Add("WilliamsRange");

            if (_chartMaster.GetChartArea("TradeArea") == null)
            {
                _gridViewIndicators.Rows.Add("Trades");
            }

            _gridViewIndicators.SelectionChanged += gridViewIndicators_SelectionChanged;


            _gridViewAreas = new DataGridView();

            HostArea.Child = _gridViewAreas;

            _gridViewAreas.AllowUserToOrderColumns = false;
            _gridViewAreas.AllowUserToDeleteRows = false;
            _gridViewAreas.AllowUserToAddRows = false;
            _gridViewAreas.AllowUserToResizeRows = false;
            _gridViewAreas.RowHeadersVisible = false;
            _gridViewAreas.ReadOnly = true;
            _gridViewAreas.AllowUserToResizeColumns = false;

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.HeaderText = @"Окна на графике";
            column1.CellTemplate = new DataGridViewTextBoxCell();
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridViewAreas.Columns.Add(column1);

            string[] areas = chartMaster.GetChartAreas();

            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] != "TradeArea")
                {
                    _gridViewAreas.Rows.Add(areas[i]);
                }
            }

            _gridViewAreas.Rows.Add("NewArea");
        }

        /// <summary>
        /// пользователь сменил индикатор
        /// </summary>
        void gridViewIndicators_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Moving Average")
            {
                TextBlockDescription.Text = "Moving Average(Скользящая средняя) - линия, представляющая собой усреднённое значение цен закрытия свечек, за определённый период";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Bollinger")
            {
                TextBlockDescription.Text = "Bollinger(линии Боллинджера) - две линии, образующие канал с <нормальным> ценовым диапазоном. При построении учитывает волатильность.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Alligator")
            {
                TextBlockDescription.Text = "Alligator(Генадий) - три скользящие средние призванные показывать тренд.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AO")
            {
                TextBlockDescription.Text = "AO(Awesome Oscillator) - осциллятор показывающий скорость изменения цены.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Fractal" ||
                _gridViewIndicators.SelectedCells[0].Value.ToString() == "Fractal")
            {
                TextBlockDescription.Text = "Fractal(Фрактал) - точки, обозначающие уровни поддержки и сопротивлений";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AccumulationDistribution")
            {
                TextBlockDescription.Text = "AccumulationDistribution A/D Индикатор Накопления/Распределения ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ADX")
            {
                TextBlockDescription.Text = "ADX(Average Directional Movement Index) - три линии, показывающие скорость изменения цены. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ATR")
            {
                TextBlockDescription.Text = "ATR(Average true range — Средний истинный диапазон) - индикатор волатильности";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "CMO")
            {
                TextBlockDescription.Text = "Chande Momentum Oscillator, CMO - осциллятор скорости изменения цены ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Force Index")
            {
                TextBlockDescription.Text = "Elder Force Oscillator - используется для измерения силы быков при росте цены и силу медведей при падении.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "OnBalanceVolume")
            {
                TextBlockDescription.Text = "OnBalanceVolume OBV";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "StochasticOscillator")
            {
                TextBlockDescription.Text = "StochasticOscillator - Стохастический осциллятор - это индикатор, который показывает отношение текущей цены закрытия к максимуму/минимуму за установленный период.";
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "RSI")
            {
                TextBlockDescription.Text = "RSI(Индекс относительной силы. relative strength index) - осциллятор,  определяющий силу тренда.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "ROC")
            {
                TextBlockDescription.Text = "ROC -Индикатор Rate of Change рассчитывается, как сравнение текущей цены с ценой прошлого периода, отстоящего от текущего на N периодов. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "RVI")
            {
                TextBlockDescription.Text = "Relative Vigor Index. «Индекс относительной бодрости» от Джона Элреса.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BFMFI")
            {
                TextBlockDescription.Text = "Индекс Облегчения Рынка (Market Facilitation Index, BW MFI)  показывает изменение цены, приходящееся на один тик. Создан Билом Вильямсом";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BullsPower")
            {
                TextBlockDescription.Text = "Bulls Power - Элдер разработал Bulls Power как разницу между максимальной ценой и 13-периодной экспоненциальной скользящей средней";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "BearsPower")
            {
                TextBlockDescription.Text = "Bears Power - Элдер разработал Bears Power как разницу между минимальной ценой и 13-периодной экспоненциальной скользящей средней";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Volume Power")
            {
                TextBlockDescription.Text = "Две линии отражающие суммарный объём направленных сделок за определённое время";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Volume")
            {
                TextBlockDescription.Text = "Volume - суммарный объём операций.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Trades")
            {
                TextBlockDescription.Text = "Trades - последние 500 тиков по инструменту";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Price Channel")
            {
                TextBlockDescription.Text = "Канал, построенный по максимумам и минимумам свечей за определённый канал";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Price Oscillator")
            {
                TextBlockDescription.Text = "Разница между двумя скользящими средними, выраженная в процентах или в пунктах";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Price Oscillator")
            {
                TextBlockDescription.Text = "Разница между двумя скользящими средними, выраженная в процентах или в пунктах";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MACD Histogram")
            {
                TextBlockDescription.Text = "MACD Histogram (Moving Average Convergence-Divergence) Индикатор ковергенции-дивергенции скользящих средних. Приведённый к Гистограмме";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MACD Line")
            {
                TextBlockDescription.Text = "MACD (Moving Average Convergence-Divergence) Индикатор ковергенции-дивергенции скользящих средних";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Momentum")
            {
                TextBlockDescription.Text = "Momentum - показывает скорость изменения цен";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "MoneyFlowIndex")
            {
                TextBlockDescription.Text = "MFI - Технический Индикатор Индекс Денежных Потоков (Money Flow Index, MFI) показывает интенсивность, с которой деньги вкладываются в ценную бумагу или выводятся из нее.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Envelops")
            {
                TextBlockDescription.Text = "Envelops. Канал построенный как отклонение от скользящей средней. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Efficiency Ratio")
            {
                TextBlockDescription.Text = "Kaufman Efficiency Ratio. Индикатор волатильности. Обобщенная фрактальная эффективностьна основе книги Кауфмана «Умный Трейдинг»";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Adaptive Look Back")
            {
                TextBlockDescription.Text = "Adaptive Look Back. Индикатор волатильности от Gene Geren.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "IvashovRange")
            {
                TextBlockDescription.Text = "Индикатор волатильности рассчитывающий сглаженное отклонение от простой скользящей средней";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Ichimoku")
            {
                TextBlockDescription.Text = "Ишимоку - индикатор состоящий из группы линий";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "CCI")
            {
                TextBlockDescription.Text = "CCI(Commodity Channel Index) индикатор индекс товарного канала - линия, представляющая собой отклонение значения цены по (H+L+C)/3 от средней за период 9";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Parabolic SAR")
            {
                TextBlockDescription.Text = "Parabolic SAR(Stop and Reverse)";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Standard Deviation")
            {
                TextBlockDescription.Text = "Standard Deviation(Среднеквадратическое отклонение) - линия, представляющая собой cреднеквадратическое отклонение значение цен закрытия свечек, за определённый период";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Volume Oscillator")
            {
                TextBlockDescription.Text = "Volume Oscillator (Осциллятор объема) представляет из себя разность двух МА объема сделок по инструменту";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "AC")
            {
                TextBlockDescription.Text = "AC (Acceleration/Deceleration, AC)  измеряет ускорение и замедление цены. Индикатор разработан Билом Вильямсом.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "VerticalHorizontalFilter")
            {
                TextBlockDescription.Text = "VerticalHorizontalFilter Вертикальный горизонтальный фильтр (VHF) показывает, в какой фазе находится рынок: в фазе направленного движения или застоя.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "WilliamsRange")
            {
                TextBlockDescription.Text = "Williams Percent Range.  Индикатор перекупленности либо перепроданности. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Trix")
            {
                TextBlockDescription.Text = "Triple Exponential Moving Average. Индикатор импульса, предназначенный для фильтрации трендового движения цены актива от рыночного шума в части большого тренда.";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Unk")
            {
                TextBlockDescription.Text = "";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "TradeThread")
            {
                TextBlockDescription.Text = "TradeThread. Объемно-тиковый осциллятор потока контрактов. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Pivot")
            {
                TextBlockDescription.Text = "Pivot Camarilla. Индикатор рассчитывающий уровни поддержки и сопротивления. ";
            }
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Pivot Points")
            {
                TextBlockDescription.Text = "Pivot Points. Индикатор рассчитывающий уровни поддержки и сопротивления на основании High, Low, Close предидущего торгового дня ";
            }


        }

        public IIndicatorCandle IndicatorCandle;

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
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
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Trades")
            {
                _chartMaster.CreateTickChart();
            }

            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "Moving Average")
            {
                string name = "";

                for (int i = 0; i < 30; i ++)
                {
                    if (_chartMaster.IndicatorIsCreate(_chartMaster.Name + "Sma" + i) == false)
                    {
                        name = "Sma" + i;
                        break;
                    }
                }
                IndicatorCandle = new MovingAverage(_chartMaster.Name + name,true);
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
            if (_gridViewIndicators.SelectedCells[0].Value.ToString() == "StochasticOscillator")
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
                IndicatorCandle = new  Ichimoku(_chartMaster.Name + name, true);
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


            Close();

            if (IndicatorCandle != null)
            {
                IndicatorCandle.ShowDialog();
            }

        }
    }
}
