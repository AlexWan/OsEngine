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

namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// Trend strategy based on 2 indicators Sma and RSI
    /// Трендовая стратегия на основе 2х индикаторов Sma и RSI
    /// </summary>
    public class SmaStochastic : BotPanel
    {
        public SmaStochastic(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _sma = new MovingAverage(name + "Sma", false);
            _sma = (MovingAverage)_tab.CreateCandleIndicator(_sma, "Prime");
            _sma.Save();

            _stoc = new StochasticOscillator(name + "ST", false);
            _stoc = (StochasticOscillator)_tab.CreateCandleIndicator(_stoc, "StocArea");
            _stoc.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Step = 500;
            Upline = 70;
            Downline = 30;


            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "SmaStochastic";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            SmaStochasticUi ui = new SmaStochasticUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        private MovingAverage _sma;

        private StochasticOscillator _stoc;

        //settings настройки публичные

        /// <summary>
        /// up Stochastic line to trade
        /// верхняя граница стохастика
        /// </summary>
        public decimal Upline;

        /// <summary>
        /// down Stochastic line to trade
        /// нижняя граница стохастика
        /// </summary>
        public decimal Downline;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// step
        /// Шаг 
        /// </summary>
        public decimal Step;

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
                    writer.WriteLine(Step);
                    writer.WriteLine(Upline);
                    writer.WriteLine(Downline);

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
                    Step = Convert.ToDecimal(reader.ReadLine());
                    Upline = Convert.ToDecimal(reader.ReadLine());
                    Downline = Convert.ToDecimal(reader.ReadLine());

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

        private decimal _lastClose;
        private decimal _firstLastRsi;
        private decimal _secondLastRsi;
        private decimal _lastSma;

        // logic логика

        /// <summary>
        /// candles finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_sma.Values == null || _stoc.ValuesUp == null ||
                _sma.Lenght + 3 > _sma.Values.Count || _stoc.P1 + 3 > _stoc.ValuesUp.Count)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _firstLastRsi = _stoc.ValuesUp[_stoc.ValuesUp.Count - 1];
            _secondLastRsi = _stoc.ValuesUp[_stoc.ValuesUp.Count - 2];
            _lastSma = _sma.Values[_sma.Values.Count - 1];

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
            if (_lastClose > _lastSma + Step && _secondLastRsi <= Downline && _firstLastRsi >= Downline &&
                Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
            }

            if (_lastClose < _lastSma - Step && _secondLastRsi >= Upline && _firstLastRsi <= Upline &&
                Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastClose < _lastSma - Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastClose > _lastSma + Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);
                }
            }
        }

    }
}
