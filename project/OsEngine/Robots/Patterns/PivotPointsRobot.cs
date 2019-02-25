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
    /// trading based on the Pivot Points indicator
    /// торговля на основе индикатора Pivot Points
    /// </summary>
    public class PivotPointsRobot : BotPanel
    {
        public PivotPointsRobot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _pivot = new PivotPoints(name + "Prime", false);
            _pivot = (PivotPoints)_tab.CreateCandleIndicator(_pivot, "Prime");

            _pivot.Save();

            //_tab.CandleFinishedEvent += TradeLogic;
            _tab.CandleFinishedEvent += TradeLogic;
            Slipage = 0;
            VolumeFix = 1;
            Stop = 0.5m;
            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PivotPointsRobot";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PivotPointsRobotUi ui = new PivotPointsRobotUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// trade tab
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab;

        private PivotPoints _pivot;

        //settings публичные настройки

        /// <summary>
        /// stop percent
        /// размер стопа в %
        /// </summary>
        public decimal Stop;

        /// <summary>
        /// slippage
        ///  проскальзывание
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
                    writer.WriteLine(Stop);

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
                    Stop = Convert.ToDecimal(reader.ReadLine());

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

        private decimal _lastPriceO;
        private decimal _lastPriceC;
        private decimal _pivotR1;
        private decimal _pivotR3;
        private decimal _pivotS1;
        private decimal _pivotS3;

        // logic логика

        private void TradeLogic(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            _lastPriceO = candles[candles.Count - 1].Open;
            _lastPriceC = candles[candles.Count - 1].Close;
            _pivotR1 = _pivot.ValuesR1[_pivot.ValuesR1.Count - 1];

            _pivotS1 = _pivot.ValuesS1[_pivot.ValuesS1.Count - 1];


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
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> openPositions)
        {
            if (_lastPriceC > _pivotR1 && _lastPriceO < _pivotR1 && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
                _pivotR3 = _pivot.ValuesR3[_pivot.ValuesR3.Count - 1];
            }

            if (_lastPriceC < _pivotS1 && _lastPriceO > _pivotS1 && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
                _pivotS3 = _pivot.ValuesS3[_pivot.ValuesS3.Count - 1];
            }
        }

        /// <summary>
        /// logic close position
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position openPosition)
        {
            if (openPosition.Direction == Side.Buy)
            {
                if (_lastPriceC > _pivotR3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC - Slipage, openPosition.OpenVolume);
                }
                if (_lastPriceC < openPosition.EntryPrice - openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }

            if (openPosition.Direction == Side.Sell)
            {
                if (_lastPriceC < _pivotS3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC + Slipage, openPosition.OpenVolume);
                }
                if (_lastPriceC > openPosition.EntryPrice + openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }
        }
    }
}
