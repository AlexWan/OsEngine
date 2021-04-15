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
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Patterns
{
    /// <summary>
    /// Trend strategy based on the pinbar formation and the breaking of Sma
    /// Трендовая стратегия на основе свечной формации пинбара и пробития Sma
    /// </summary>
    public class PinBarTrade : BotPanel
    {
        public PinBarTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Sma = new MovingAverage(name + "MA", false);
            Sma = (MovingAverage)_tab.CreateCandleIndicator(Sma, "Prime");
            Sma.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;

            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PinBarTrade";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PinBarTradeUi ui = new PinBarTradeUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        public MovingAverage Sma;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

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
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
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
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
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
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        private void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastClose;
        private decimal _lastOpen;
        private decimal _lastHigh;
        private decimal _lastLow;

        private decimal _lastSma;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (Sma.Values.Count < Sma.Lenght + 2)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _lastOpen = candles[candles.Count - 1].Open;
            _lastHigh = candles[candles.Count - 1].High;
            _lastLow = candles[candles.Count - 1].Low;
            _lastSma = Sma.Values[Sma.Values.Count - 1];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// logic open position
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastClose >= _lastHigh - ((_lastHigh - _lastLow) / 3) && _lastOpen >= _lastHigh - ((_lastHigh - _lastLow) / 3)
                && _lastSma < _lastClose 
                && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
            }
            if (_lastClose <= _lastLow + ((_lastHigh - _lastLow) / 3) && _lastOpen <= _lastLow + ((_lastHigh - _lastLow) / 3)
                && _lastSma > _lastClose 
                && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose + Slipage);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State != PositionStateType.Open ||
                position.CloseActiv == true ||
                (position.CloseOrders != null && position.CloseOrders.Count > 0))
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastClose <= _lastLow + ((_lastHigh - _lastLow) / 3) && _lastOpen <= _lastLow + ((_lastHigh - _lastLow) / 3)
                     && _lastSma > _lastClose)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);

                    if (Regime == BotTradeRegime.On)  // Fix: Открываем реверсивную сделку только если торгуем и в лонг и в шорт
                    {
                        _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
                    }
                }
            }

            else if (position.Direction == Side.Sell)
            {
                if (_lastClose >= _lastHigh - ((_lastHigh - _lastLow) / 3) && _lastOpen >= _lastHigh - ((_lastHigh - _lastLow) / 3)
                     && _lastSma < _lastClose)
                {
                    _tab.CloseAtLimit(position, _lastClose + Slipage, position.OpenVolume);

                    if (Regime == BotTradeRegime.On)  // Fix: Открываем реверсивную сделку только если торгуем и в лонг и в шорт
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
                    }
                }
            }
        }
    }
}
