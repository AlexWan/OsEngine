/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*
Description

trading robot for osengine

Bot - trading pairs in the trend
If the correlation is below -0.8 and we are on some side of the cointegration - enter counting on a further spread
Exit - when correlation rises above 0.8

Ru
Бот - ловящий кочергу в парах в тренд
Суть идеи - если корреляция ниже -0.8 и мы с какой-то стороны коинтеграции - входим рассчитывая на дальнейшее раздвижение
Выход - когда корреляция повышается больше 0.8. Т.е. раздвижение инструментов закончено

*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCorrelationNegative")]
    public class PairCorrelationNegative : BotPanel
    {
        public PairCorrelationNegative(string name, StartProgram startProgram)
          : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            MaxPositionsCount = CreateParameter("Max poses count", 5, 5, 5, 5);

            MaxCorrelationToEntry = CreateParameter("Max Correlation To Entry", -0.8m, -0.1m, 5,0.1m);

            MinCorrelationToExit = CreateParameter("Min Correlation To Exit", 0.8m, 0.1m, 1, 0.1m);

            Description = "Bot - trading pairs in the trend " +
                "If the correlation is below -0.8 and we are on some side of the cointegration -" +
                " enter counting on a further spread. " +
                "Exit - when correlation rises above 0.8";

        }

        BotTabPair _pairTrader;

        public override string GetNameStrategyType()
        {
            return "PairCorrelationNegative";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private StrategyParameterInt MaxPositionsCount;

        private StrategyParameterDecimal MaxCorrelationToEntry;

        private StrategyParameterDecimal MinCorrelationToExit;

        public StrategyParameterString Regime;

        private void _pairTrader_CorrelationChangeEvent(System.Collections.Generic.List<PairIndicatorValue> correlation, PairToTrade pair)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (pair.HavePositions)
            {
                ClosePositionLogic(pair);
            }
            else
            {
                OpenPositionLogic(pair);
            }
        }

        private void ClosePositionLogic(PairToTrade pair)
        {
            if (pair.CorrelationLast > MinCorrelationToExit.ValueDecimal)
            {
                pair.ClosePositions();
            }
        }

        private void OpenPositionLogic(PairToTrade pair)
        {
            if (pair.CorrelationLast > MaxCorrelationToEntry.ValueDecimal)
            {
                return;
            }

            if (_pairTrader.PairsWithPositionsCount >= MaxPositionsCount.ValueInt)
            {
                return;
            }

            if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.BuySec1SellSec2();
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.SellSec1BuySec2();
            }
        }

    }
}