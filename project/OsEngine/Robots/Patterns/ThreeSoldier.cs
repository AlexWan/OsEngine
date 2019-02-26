/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Patterns
{
    /// <summary>
    /// Trading robot Three Soldiers. When forming a pattern of three growing / falling candles, the entrance to the countertrend with a fixation on a profit or a stop
    /// Торговый робот ТриСрлдата. При формироваваниии паттерна из трех растущих/падующих свечей вход по в контртренд с фиксацией по тейку или по стопу
    /// </summary>
    public class ThreeSoldier : BotPanel
    {

        public ThreeSoldier(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += Strateg_ClosePosition;

            Slipage = 10;
            VolumeFix = 1;
            HeightSoldiers = 1;
            MinHeightSoldier = 1;
            ProcHeightTake = 30;
            ProcHeightStop = 10;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// name bot
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ThreeSoldier";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            ThreeSoldierUi ui = new ThreeSoldierUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// trading tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //settings настройки публичные

        /// <summary>
        /// total pattren height
        /// общая высота паттрена
        /// </summary>
        public decimal HeightSoldiers;

        /// <summary>
        /// the minimum height of the candle in patten
        /// минимальная высота свечи в паттрене
        /// </summary>
        public decimal MinHeightSoldier;

        /// <summary>
        /// Profit order length %
        /// процент от высоты паттрена на закрытие по тейку
        /// </summary>
        public decimal ProcHeightTake;

        /// <summary>
        /// Stop order length %
        /// процент от высоты паттрена на закрытие по стопу
        /// </summary>
        public decimal ProcHeightStop;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа позицию
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
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(HeightSoldiers);
                    writer.WriteLine(MinHeightSoldier);
                    writer.WriteLine(ProcHeightTake);
                    writer.WriteLine(ProcHeightStop);

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
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    HeightSoldiers = Convert.ToInt32(reader.ReadLine());
                    MinHeightSoldier = Convert.ToInt32(reader.ReadLine());
                    ProcHeightTake = Convert.ToInt32(reader.ReadLine());
                    ProcHeightStop = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete save files
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

        // logic / логика

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

            if (candles.Count < 3)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

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
        /// closing logic
        /// логика закрытия позиции
        /// </summary>
        private void Strateg_ClosePosition(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 3].Open - _tab.CandlesAll[_tab.CandlesAll.Count - 1].Close);
                    decimal priceStop = _lastPrice - (heightPattern * ProcHeightStop) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * ProcHeightTake) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop - Slipage);
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake - Slipage);
                }
                else
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 1].Close - _tab.CandlesAll[_tab.CandlesAll.Count - 3].Open);
                    decimal priceStop = _lastPrice + (heightPattern * ProcHeightStop) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * ProcHeightTake) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop + Slipage);
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake + Slipage);
                }
            }
        }


        /// <summary>
        /// logic open position
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close) < HeightSoldiers) return;
                if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close) < MinHeightSoldier) return;
                if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close) < MinHeightSoldier) return;
                if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close) < MinHeightSoldier) return;

                //  long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
                return;
            }
        }

    }
}
