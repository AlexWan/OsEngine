using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OsEngine.Indicators;
using System.Drawing;
using OsEngine.Logging;

namespace OsEngine.Robots.MoiRoboti
{
    /// <summary>
    /// основной класс (модель) с интерфейсом INotifyPropertyChanged
    /// </summary>
    public class Breakdown : BotPanel, INotifyPropertyChanged 
    {
        /// <summary>
        ///  свойства и поля для байдинга данных на форму
        /// </summary>
        private decimal index; 
        public decimal Index // свойство переменной (значение)
        {
            get => index;
            set => Set(ref index, value);
        }

        private decimal _price; // поле хранения цены
        public decimal Price   // свойство цены
        {
            get => _price;
            set => Set(ref _price, value);
        }

        // настройки публичные
        /// <summary>
        /// объём для входа
        /// </summary>
        public decimal Volume = 1.1m; // тестовое присвоение значения
        /// <summary>
        /// проскальзывание на открытие первый ордер
        /// </summary>
        public int SlipageOpenFirst;

        /// <summary>
        /// вкладка робота для торговли
        /// </summary>
        private BotTabSimple _tab; // поле хранения вкладки робота 

        /// <summary>
        ///  конструктор робота
        /// </summary>
        public Breakdown(string name, StartProgram startProgram) : base(name, startProgram) 
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле

            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent; // для тестовой логики
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

                                                             // создаем индикатор дампинга и машки для расчета его индекса 
            _dampIndex = new Line(name + "dampIndex", false)
            {
                ColorBase = Color.DodgerBlue,
                PaintOn = true,
            };
            _dampIndex = _tab.CreateCandleIndicator(_dampIndex, "dampArea");
            _dampIndex.Save();

            _smaHighIndex = new MovingAverage(name + "MovingHighIndex", false)
            {
                Lenght = 5,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHighIndex = _tab.CreateCandleIndicator(_smaHighIndex, "Prime");
            _smaHighIndex.Save();

            _smaHigh2Period = new MovingAverage(name + "MovingHigh2Period", false)
            {
                Lenght = 2,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHigh2Period = _tab.CreateCandleIndicator(_smaHigh2Period, "Prime");
            _smaHigh2Period.Save();

            _smaHigh3Period = new MovingAverage(name + "MovingHigh3Period", false)
            {
                Lenght = 3,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHigh3Period = _tab.CreateCandleIndicator(_smaHigh3Period, "Prime");
            _smaHigh3Period.Save();

            _smaHigh4Period = new MovingAverage(name + "MovingHigh4Period", false)
            {
                Lenght = 4,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.High
            };
            _smaHigh4Period = _tab.CreateCandleIndicator(_smaHigh4Period, "Prime");
            _smaHigh4Period.Save();

            _smaLowIndex = new MovingAverage(name + "MovingLowIndex", false)
            {
                Lenght = 5,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLowIndex = _tab.CreateCandleIndicator(_smaLowIndex, "Prime");
            _smaLowIndex.Save();

            _smaLow2Period = new MovingAverage(name + "Moving2LowPeriod", false)
            {
                Lenght = 2,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow2Period = _tab.CreateCandleIndicator(_smaLow2Period, "Prime");
            _smaLow2Period.Save();

            _smaLow3Period = new MovingAverage(name + "Moving3LowPeriod", false)
            {
                Lenght = 3,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow3Period = _tab.CreateCandleIndicator(_smaLow3Period, "Prime");
            _smaLow3Period.Save();

            _smaLow4Period = new MovingAverage(name + "Moving4LowPeriod", false)
            {
                Lenght = 4,
                ColorBase = Color.DodgerBlue,
                PaintOn = false,
                TypePointsToSearch = PriceTypePoints.Low
            };
            _smaLow4Period = _tab.CreateCandleIndicator(_smaLow4Period, "Prime");
            _smaLow4Period.Save();
        }

        /// <summary>
        ///  событие получения цены, использую для обновления переменных
        /// </summary>
        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            Price = _tab.MarketDepth.Bids[0].Price; // обновляем значение цены
            if (_index != null)
            {
                Index = ((Line)_dampIndex).Values[((Line)_dampIndex).Values.Count - 1]; // обновляем значение в индексе
                ReloadDampIndex(); // перегрузка  
            }
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles) // событие создания свечи 
        {
            ReloadDampIndex(); // первая перегрузка индекса
            TryOpenPosition(candles);
            Strateg(candles);
        }
        /// <summary>
        /// главный вход в логику робота. Вызывается когда на рынке заканчивается свеча
        /// </summary>
        void Strateg(List<Candle> candles)
        {
            ReloadDampIndex();

            if (candles.Count < 14)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            {
                for (int i = 0; positions != null && i < positions.Count; i++)
                {
                    positions[i].StopOrderIsActiv = false;
                    positions[i].ProfitOrderIsActiv = false;
                }
            }

            if (positions == null || positions.Count == 0)
            {
                TryOpenPosition(candles);
            }
            else
            {
                TryClosePosition(positions[0], candles);
            }
        }

        /// <summary>
        ///  индикаторы для дампинг стратегии
        /// </summary>
        private IIndicator _smaHighIndex;
        private IIndicator _smaLowIndex;
        private IIndicator _smaHigh2Period;
        private IIndicator _smaHigh3Period;
        private IIndicator _smaHigh4Period;
        private IIndicator _smaLow2Period;
        private IIndicator _smaLow3Period;
        private IIndicator _smaLow4Period;
        private IIndicator _dampIndex;

        // расчёт индикатора DampIndex

        /// <summary>
        /// damp index
        /// </summary>
        private List<decimal> _index;

        /// <summary>
        /// перезагрузить индикатор дамп индекс
        /// </summary>
        private void ReloadDampIndex()
        {
            if (_index == null)
            {
                _index = new List<decimal>();
            }

            if (((MovingAverage)_smaHighIndex).Values.Count - 1 == _index.Count)
            {
                // обновляем только последнее значение
                _index.Add(GetDapmIndex(((MovingAverage)_smaHighIndex).Values.Count - 1));
            }
            else
            {
                _index = new List<decimal>();
                for (int i = 0; i < ((MovingAverage)_smaHighIndex).Values.Count; i++)
                {
                    _index.Add(GetDapmIndex(i));
                }
            }

            ((Line)_dampIndex).ProcessDesimals(_index);
        }

        /// <summary>
        /// взять дапм индекс по индексу
        /// </summary>
        private decimal GetDapmIndex(int index)
        {
            decimal result = 0;

            try
            {
                if (index - ((MovingAverage)_smaHighIndex).Lenght >= 0 &&
                    index - ((MovingAverage)_smaLowIndex).Lenght >= 0)
                {
                    decimal smaHigh = ((MovingAverage)_smaHighIndex).Values[index];
                    decimal smaHigh2 = ((MovingAverage)_smaHighIndex).Values[index - ((MovingAverage)_smaHighIndex).Lenght];
                    decimal smaLow = ((MovingAverage)_smaLowIndex).Values[index];
                    decimal smaLow2 = ((MovingAverage)_smaLowIndex).Values[index - ((MovingAverage)_smaLowIndex).Lenght];

                    if (smaHigh2 - smaLow2 != 0)
                    {
                        result = Math.Round((smaHigh - smaLow) / (smaHigh2 - smaLow2), 4);
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
            return result;
        }
        /// <summary>
        /// взять цену для стопПриказа
        /// </summary>
        /// <param name="side">сторона входа</param>
        /// <param name="index">индекс свечи</param>
        /// <returns>цена стопПриказа</returns>
        private decimal GetPriseStop(Side side, int index)
        {
            List<Candle> myCandles = _tab.CandlesAll;

            if (myCandles == null)
            {
                return 0;
            }


            if (side == Side.Buy)
            { // стоп для лонга
                decimal min = decimal.MaxValue;
                for (int i = index; i > -1 && i > index; i--)
                {
                    if (myCandles[i].Low < min)
                    {
                        min = myCandles[i].Low;
                    }
                }
                return min;// + _tab.Securiti.PriceStep;
            }
            else
            { // стоп для шорта
                decimal max = decimal.MinValue;
                for (int i = index; i > -1 && i > index; i--)
                {
                    if (myCandles[i].High > max)
                    {
                        max = myCandles[i].High;
                    }
                }
                return max;// - _tab.Securiti.PriceStep;
            }
        }


        /// <summary>
        /// проверить условия на вход и войти в позицию
        /// </summary>
        private void TryOpenPosition(List<Candle> candles)
        {
            decimal damping;
            decimal avgHigh2;
            decimal avgLow3;
            decimal avgLow2;
            decimal avgHigh3;
            decimal avgHighShifted;
            decimal avgLowShifted;
            try
            {
                if (((MovingAverage)_smaHigh4Period).Values.Count - 1 - 10 < 0)
                {
                    return;
                }
                damping = ((Line)_dampIndex).Values[((Line)_dampIndex).Values.Count - 1];
                avgHigh2 = ((MovingAverage)_smaHigh2Period).Values[((MovingAverage)_smaHigh2Period).Values.Count - 1]; // SMA.Series(High, 2);
                avgLow3 = ((MovingAverage)_smaLow3Period).Values[((MovingAverage)_smaLow3Period).Values.Count - 1]; //SMA.Series(Low, 3);
                avgLow2 = ((MovingAverage)_smaLow3Period).Values[((MovingAverage)_smaLow3Period).Values.Count - 1]; //SMA.Series(Low, 3);
                avgHigh3 = ((MovingAverage)_smaHigh3Period).Values[((MovingAverage)_smaHigh3Period).Values.Count - 1];//SMA.Series(High, 3);
                avgHighShifted = ((MovingAverage)_smaHigh4Period).Values[((MovingAverage)_smaHigh4Period).Values.Count - 1 - 10];//SMA.Series(High, 4) >> 10;
                avgLowShifted = ((MovingAverage)_smaLow4Period).Values[((MovingAverage)_smaLow4Period).Values.Count - 1 - 10]; //SMA.Series(Low, 4) >> 10;
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
                return;
            }
 
            if (damping < 1 && (candles[candles.Count - 1].Close > avgHigh2) && (avgLow3 > avgHighShifted))
            {
                _tab.SetNewLogMessage("Системный вход в лонг. Время: " + candles[candles.Count - 1].TimeStart, LogMessageType.Signal);

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.BuyAtMarket(Volume);
                }
                else
                {
                    _tab.BuyAtLimit(Volume, candles[candles.Count - 1].Close + _tab.Securiti.PriceStep * SlipageOpenFirst); // ЗДЕСЬ!!!!!!!!!!!!!!
                }
                return;
            }
            else if (damping < 1 && (candles[candles.Count - 1].Close < avgLow2) && (avgHigh3 < avgLowShifted))
            {
                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.SellAtMarket(Volume);
                }
                else
                {
                    _tab.SellAtLimit(Volume, candles[candles.Count - 1].Close - _tab.Securiti.PriceStep * SlipageOpenFirst); // ЗДЕСЬ!!!!!!!!!!!!!!
                }
                return;
            }
            else if (damping <= 1)
            {            }
        }
        /// <summary>
        /// выставить стоп приказ по открытой позиции
        /// </summary>
        private void TryClosePosition(Position position, List<Candle> candles)
        {
            if (position.Direction == Side.Buy)
            {
                decimal priceEtalon = GetPriseStop(Side.Buy, candles.Count - 1);

                decimal priceOrder = priceEtalon - _tab.Securiti.PriceStep; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = priceEtalon * _tab.Securiti.PriceStep;

                if (priceRedLine - _tab.Securiti.PriceStep * 10 > _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

            }

            if (position.Direction == Side.Sell)
            {
                decimal priceEtalon = GetPriseStop(Side.Sell, candles.Count - 1);

                decimal priceOrder = priceEtalon + _tab.Securiti.PriceStep; // ЗДЕСЬ!!!!!!!!!!!!!!
                decimal priceRedLine = priceEtalon * _tab.Securiti.PriceStep;

                if (priceRedLine + _tab.Securiti.PriceStep * 10 < _tab.PriceBestAsk)
                {
                    _tab.CloseAtLimit(position, _tab.PriceBestAsk, position.OpenVolume);
                    return;
                }

                _tab.CloseAtStop(position, priceRedLine, priceOrder);
            }
        }

        public override string GetNameStrategyType()
        {
            return "Breakdown";
        }

        public override void ShowIndividualSettingsDialog()
        {
            Breakdown_Ui ui = new Breakdown_Ui(this);
            ui.Show();
        }

        /// <summary>
        ///  реализация интерфейса PropertyChanged-событий изменения свойств
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged; // событие изменения свойств
   
        protected void СallUpdate(string name)  // сигнализирует об изменении свойств
        {
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // сверяет значения любых типов данных и выдает сигнал об изменении 
        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "") 
        {
            if (!field.Equals(value))
            {
                field = value;
                СallUpdate(name);
            }
        }
    }
}
        
 