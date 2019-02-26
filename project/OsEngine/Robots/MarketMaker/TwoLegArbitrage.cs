/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MarketMaker
{
    public class TwoLegArbitrage : BotPanel
    {
        public TwoLegArbitrage(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tabIndex = TabsIndex[0];

            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];

            _tab1.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Upline = CreateParameter("Upline", 10, 50, 80, 3);
            Downline = CreateParameter("Downline", 10, 25, 50, 2);
            Volume = CreateParameter("Volume", 3, 1, 50, 4);
            Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
            RsiLength = CreateParameter("RsiLength", 10, 5, 150, 2);

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tabIndex.CreateCandleIndicator(_rsi, "RsiArea");
            _rsi.Lenght = RsiLength.ValueInt;
            _rsi.Save();

            ParametrsChangeByUser += TwoLegArbitrage_ParametrsChangeByUser;
        }

        /// <summary>
        /// user change params
        /// пользователь изменил параметр
        /// </summary>
        void TwoLegArbitrage_ParametrsChangeByUser()
        {
            if (_rsi.Lenght != RsiLength.ValueInt)
            {
                _rsi.Lenght = RsiLength.ValueInt;
                _rsi.Reload();
            }
        }

        /// <summary>
        /// name bot
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TwoLegArbitrage";
        }


        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// index tab
        /// вкладка для формирования индекса
        /// </summary>
        private BotTabIndex _tabIndex;

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// RSI
        /// </summary>
        private Rsi _rsi;

        //settings / настройки публичные

        /// <summary>
        /// slippage / проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// regime
        /// режим работы робота
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// volume
        /// объём исполняемый в одной сделке
        /// </summary>
        public StrategyParameterInt Volume;

        /// <summary>
        /// upper line for RSI for decision making
        /// верхняя граница для RSI для принятия решений
        /// </summary>
        public StrategyParameterInt Upline;

        /// <summary>
        /// lower line for RSI for decision making
        /// нижняя граница для RSI для принятия решений
        /// </summary>
        public StrategyParameterInt Downline;

        /// <summary>
        /// Rsi length
        /// длинна RSI
        /// </summary>
        public StrategyParameterInt RsiLength;

        private decimal _lastRsi;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_tabIndex.Candles.Count == 0 ||
                _tab1.CandlesFinishedOnly.Count != _tab2.CandlesFinishedOnly.Count)
            {
                return;
            }

            if (_rsi.Values == null)
            {
                return;
            }

            _lastRsi = _rsi.Values[_rsi.Values.Count - 1];

            if (_rsi.Values == null || _rsi.Values.Count < _rsi.Lenght + 5)
            {
                return;

            }

            for (int j = 0; TabsSimple.Count != 0 && j < TabsSimple.Count; j++)
            {
                List<Position> openPositions = TabsSimple[j].PositionsOpenAll;
                if (openPositions != null && openPositions.Count != 0)
                {
                    for (int i = 0; i < openPositions.Count; i++)
                    {
                        LogicClosePosition(openPositions[i], TabsSimple[j]);
                    }
                }
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                if (openPositions == null || openPositions.Count == 0)
                {
                    LogicOpenPosition(TabsSimple[j]);
                }
            }
        }

        /// <summary>
        /// logic opening first position
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(BotTabSimple tab)
        {
            if (_lastRsi > Upline.ValueInt && Regime.ValueString != "OnlyLong")
            {
                tab.SellAtLimit(Volume.ValueInt, tab.PriceBestBid - Slippage.ValueInt * tab.Securiti.PriceStep);
            }
            if (_lastRsi < Downline.ValueInt && Regime.ValueString != "OnlyShort")
            {
                tab.BuyAtLimit(Volume.ValueInt, tab.PriceBestAsk + Slippage.ValueInt * tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(Position position, BotTabSimple tab)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastRsi > Upline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestBid - Slippage.ValueInt * tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
            if (position.Direction == Side.Sell)
            {
                if (_lastRsi < Downline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestAsk + Slippage.ValueInt * tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
        }

    }
}
