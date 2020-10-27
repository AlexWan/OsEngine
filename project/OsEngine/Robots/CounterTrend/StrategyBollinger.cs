/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.CounterTrend
{

    [Bot("Bollinger")]
    public class StrategyBollinger : BotPanel
    {
        public StrategyBollinger(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = new Bollinger(name + "Bollinger", false);
            _bollinger = _tab.CreateIndicator(_bollinger);

            _moving = new MovingAverage(name + "Moving", false) { Lenght = 15 };
            _moving = _tab.CreateIndicator(_moving);

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Slippage = 0;
            Volume = 1;
            Regime = BotTradeRegime.On;

            DeleteEvent += Strategy_DeleteEvent;

            Load();
        }

        /// <summary>
        /// take the name of the strategy
        /// взять имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Bollinger";
        }

        /// <summary>
        /// show settings window
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            StrategyBollingerUi ui = new StrategyBollingerUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// tab through which trade is conducted
        /// вкладка через которую ведётся торговля
        /// </summary>
        private BotTabSimple _tab;

        // indicators индикаторы

        /// <summary>
        /// bollinger
        /// боллинжер
        /// </summary>
        private Bollinger _bollinger;

        /// <summary>
        /// MA
        /// мувинг
        /// </summary>
        private MovingAverage _moving;

        // public settings / настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slippage;

        /// <summary>
        /// volume
        /// объём входа
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slippage);
                    writer.WriteLine(Volume);
                    writer.WriteLine(Regime);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                    Slippage = Convert.ToDecimal(reader.ReadLine());
                    Volume = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete file with save
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // logic / логика

        /// <summary>
        /// candle completion event
        /// событие завершения свечи
        /// </summary>
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {

            if (Regime == BotTradeRegime.Off)
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
        /// position opening logic
        /// логика открытия позиции
        /// </summary>
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
                _tab.SellAtLimit(Volume, close - Slippage);
            }

            if (close < bollingerDownLast
                && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(Volume, close + Slippage);
            }
        }

        /// <summary>
        /// position closing logic
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
                    _tab.CloseAtLimit(position, lastClose - Slippage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (lastClose < moving)
                {
                    _tab.CloseAtLimit(position, lastClose + Slippage, position.OpenVolume);
                }
            }
        }
    }
}
