using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    /// <summary>
    /// стратегия основанная на линиях боллинджера
    /// </summary>
    public class StrategyBollinger : BotPanel
    {

        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyBollinger(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = new Bollinger(name + "Bollinger", false);
            _bollinger = (Bollinger)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.Save();

            _moving = new MovingAverage(name + "Moving", false) { Lenght = 15 };
            _moving = (MovingAverage)_tab.CreateCandleIndicator(_moving, "Prime");
            _moving.Save();

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Slipage = 0;
            Volume = 1;
            Regime = BotTradeRegime.On;

            DeleteEvent += Strategy_DeleteEvent;

            Load();
        }

        /// <summary>
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Bollinger";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            StrategyBollingerUi ui = new StrategyBollingerUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка через которую ведётся торговля
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// боллиндер
        /// </summary>
        private Bollinger _bollinger;

        /// <summary>
        /// мувинг
        /// </summary>
        private MovingAverage _moving;

        // настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// объём входа
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(Volume);
                    writer.WriteLine(Regime);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    Volume = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {

            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && DateTime.Now.Hour < 10)
            {
                return;
            }

            if (_bollinger.ValuesUp == null ||
                _bollinger.ValuesUp.Count == 0 ||
                _bollinger.ValuesUp.Count < candles.Count ||
                _moving.Values.Count < candles.Count)
            {
                return;
            }

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                for (int i = 0; i < openPosition.Count; i++)
                {
                    LogicClosePosition(openPosition[i], candles);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition(candles);
            }
        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        /// <param name="candles"></param>
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal bollingerUpLast = _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1];

            decimal bollingerDownLast = _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1];

            if (bollingerUpLast == 0 ||
                bollingerDownLast == 0)
            {
                return;
            }

            decimal close = candles[candles.Count - 1].Close;

            if (close > bollingerUpLast
                && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(Volume, close - Slipage);
            }

            if (close < bollingerDownLast
                && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(Volume, close + Slipage);
            }
        }

        /// <summary>
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            if (position.State == PositionStateType.Closing)
            {
                return;
            }
            decimal moving = _moving.Values[_moving.Values.Count - 1];

            decimal lastClose = candles[candles.Count - 1].Close;

            if (position.Direction == Side.Buy)
            {
                if (lastClose > moving)
                {
                    _tab.CloseAtLimit(position, lastClose - Slipage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (lastClose < moving)
                {
                    _tab.CloseAtLimit(position, lastClose + Slipage, position.OpenVolume);
                }
            }
        }
    }
}
