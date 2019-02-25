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
    /// Overbought / Oversold RSI Contrand Strategy with Trend Filtering via MovingAverage
    /// конттрендовая стратегия по перекупленности/перепроданности RSI с фильтром по тренду через MovingAverage
    /// </summary>
    public class RsiContrtrend : BotPanel
    {
        public RsiContrtrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _ma = new MovingAverage(name + "MA", false) { Lenght = 50, ColorBase = Color.CornflowerBlue, };
            _ma = (MovingAverage)_tab.CreateCandleIndicator(_ma, "Prime");

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tab.CreateCandleIndicator(_rsi, "RsiArea");

            Upline = new LineHorisontal("upline", "RsiArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "RsiArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _rsi.Save();
            _ma.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = 65;
            Downline.Value = 35;


            Load();

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RsiContrtrend";
        }
        /// <summary>
        /// show settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            RsiContrtrendUi ui = new RsiContrtrendUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private MovingAverage _ma;

        private Rsi _rsi;

        // settings / настройки публичные

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
        /// up line
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// down line
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

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
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
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
        /// load settins
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
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
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
        private decimal _lastMa;
        private decimal _lastRsi;


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

            if (_ma.Values == null || _rsi.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastMa = _ma.Values[_ma.Values.Count - 1];
            _lastRsi = _rsi.Values[_rsi.Values.Count - 1];


            if (_ma.Values.Count < _ma.Lenght + 1 || _rsi.Values == null || _rsi.Values.Count < _rsi.Lenght + 5)
            {
                return;

            }

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
            decimal lastClose = candles[candles.Count - 1].Close;
            if (_lastMa > lastClose && _lastRsi > Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
            if (_lastMa < lastClose && _lastRsi < Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            if (position.Direction == Side.Buy)
            {
                if (lastClose < _lastMa || _lastRsi > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                }
            }
            if (position.Direction == Side.Sell)
            {
                if (lastClose > _lastMa || _lastRsi < Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                }
            }
        }

    }

}
