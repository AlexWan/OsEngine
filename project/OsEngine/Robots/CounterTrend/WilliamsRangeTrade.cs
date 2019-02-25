/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.CounterTrend
{
    /// <summary>
    /// Counter Trend Strategy Based on Willams% R Indicator
    /// Контртрендовая стратегия на основе индикатора Willams %R
    /// </summary>
    public class WilliamsRangeTrade : BotPanel
    {
        public WilliamsRangeTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _wr = new WilliamsRange(name + "WillamsRange", false);
            _wr = (WilliamsRange)_tab.CreateCandleIndicator(_wr, "WilliamsArea");

            Upline = new LineHorisontal("upline", "WilliamsArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "WilliamsArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _wr.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = -20;
            Downline.Value = -80;

            Load();

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "WilliamsRangeTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            WilliamsRangeTradeUi ui = new WilliamsRangeTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        /// <summary>
        /// WilliamsRange
        /// </summary>
        private WilliamsRange _wr;

        /// <summary>
        /// up line to trade
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// down line to trade
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

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
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

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
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());

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
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastWr;

        //logic логика

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

            if (_wr.Values == null || _wr.Values.Count < _wr.Nperiod + 2)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastWr = _wr.Values[_wr.Values.Count - 1];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
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
            if (_lastWr < Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastWr > Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastWr > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastWr < Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }
            }
        }
    }
}
