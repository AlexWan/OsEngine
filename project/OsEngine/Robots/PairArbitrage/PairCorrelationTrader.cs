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

Trading robot for osengine
if the correlation is higher than 0.9 and we are on some side of the cointegration - enter, counting on the pair convergence
Exit by the inverse cointegration signal

Ru
Суть - если корреляция выше 0.9 и мы с какой - то стороны коинтеграции - входим, рассчитывая на схождение пар
Выход по обратному сигналу коинтеграции
*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCorrelationTrader")]
    public class PairCorrelationTrader : BotPanel
    {
        public PairCorrelationTrader(string name, StartProgram startProgram)
           : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            MaxPositionsCount = CreateParameter("max poses count", 5, 5, 5, 5);

            Description = "if the correlation is higher than 0.9 and we are " +
                "on some side of the cointegration - enter," +
                " counting on the pair convergence " +
                "Exit by the inverse cointegration signal";

        }

        BotTabPair _pairTrader;

        public override string GetNameStrategyType()
        {
            return "PairCorrelationTrader";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private StrategyParameterInt MaxPositionsCount;

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
            if (pair.SideCointegrationValue == CointegrationLineSide.Up
                && pair.LastEntryCointegrationSide == CointegrationLineSide.Down)
            {
                pair.ClosePositions();
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down
                && pair.LastEntryCointegrationSide == CointegrationLineSide.Up)
            {
                pair.ClosePositions();
            }
        }

        private void OpenPositionLogic(PairToTrade pair)
        {
            if(pair.CorrelationLast < 0.9m)
            {
                return;
            }

            if (_pairTrader.PairsWithPositionsCount >= MaxPositionsCount.ValueInt)
            {
                return;
            }

            if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.SellSec1BuySec2();
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.BuySec1SellSec2();
            }
        }

    }
}