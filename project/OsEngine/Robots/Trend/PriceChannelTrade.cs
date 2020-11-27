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
    /// Trend strategy on interseption PriceChannel indicator
    /// Трендовая стратегия на пересечение индикатора PriceChannel
    /// </summary>
    public class PriceChannelTrade : BotPanel
    {
        public PriceChannelTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _priceCh = new PriceChannel(name + "Prime", false);
            _priceCh = (PriceChannel)_tab.CreateCandleIndicator(_priceCh, "Prime");



            _priceCh.Save();

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
            return "PriceChannelTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelTradeUi ui = new PriceChannelTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private PriceChannel _priceCh;

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
                    Slipage = reader.ReadLine().ToDecimal();
                    VolumeFix = reader.ReadLine().ToDecimal();
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
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPriceC;
        private decimal _lastPriceH;
        private decimal _lastPriceL;
        private decimal _lastPriceChUp;
        private decimal _lastPriceChDown;

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

            if (_priceCh.ValuesUp == null || _priceCh.ValuesUp.Count < _priceCh.LenghtUpLine + 3
                || _priceCh.ValuesDown.Count < _priceCh.LenghtDownLine + 3)
            {
                return;
            }

            _lastPriceC = candles[candles.Count - 1].Close;
            _lastPriceH = candles[candles.Count - 1].High;
            _lastPriceL = candles[candles.Count - 1].Low;
            _lastPriceChUp = _priceCh.ValuesUp[_priceCh.ValuesUp.Count - 2];
            _lastPriceChDown = _priceCh.ValuesDown[_priceCh.ValuesDown.Count - 2];

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
            if (_lastPriceH > _lastPriceChUp &&
                _lastPriceL < _lastPriceChDown)
            {
                return;
            }

            if (_lastPriceH > _lastPriceChUp && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
            }

            if (_lastPriceL < _lastPriceChDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPriceL < _lastPriceChDown)
                {
                    _tab.CloseAtLimit(position, _lastPriceC - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong 
                        && Regime != BotTradeRegime.OnlyClosePosition
                        && _tab.PositionsOpenAll.Count < 3)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPriceH > _lastPriceChUp)
                {
                    _tab.CloseAtLimit(position, _lastPriceC + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime 
                        != BotTradeRegime.OnlyClosePosition
                        && _tab.PositionsOpenAll.Count < 3)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
                    }
                }
            }
        }
    }
}
