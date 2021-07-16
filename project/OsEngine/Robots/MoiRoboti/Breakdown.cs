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

            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent; // для тестовой логики
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

        private void _tab_CandleFinishedEvent(List<Candle> candles) // тест
        {
            ReloadDampIndex(); // перегрузка 
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
        ///  событие обновления лучшей цены создано для обновления переменных
        /// </summary>
        public void _tab_BestBidAskChangeEvent(decimal arg1, decimal arg2) 
        {
            Price = _tab.MarketDepth.Bids[0].Price; // обновляем значение цены
            if (_index != null)
            {
               Index = ((Line)_dampIndex).Values[((Line)_dampIndex).Values.Count - 1]; // обновляем значение в индексе
               ReloadDampIndex(); // перегрузка  
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
        
 