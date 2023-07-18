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

The robot trades on a chart of deviations of one instrument from another, calculated through their difference with the multiplier. 
Two lines, calculated from the standard deviation multiplied by the multiplier, are superimposed on this graph (Cointegration). Above and below zero.

When the current deviation is higher than the upper line on the deviation chart - we enter a position expecting the instruments to converge. We close the previous position.
When the current deviation is below the bottom line on the Deviation chart - enter the position, counting on the instruments convergence. Closing the previous position.

Ru
Робот торгующий по графику отклонений одного инструмента от другого, рассчитанного через их разницу с мультипликатором. 
На данный график (Коинтеграция) накладывается две линии, рассчитанные из стандартного отклонение умноженного на мультипликатор. Выше и ниже нуля.

Когда текущее отклонение выше верхней линии на графике отклонений - входим в позицию рассчитывая на схождение инструментов. Закрываем предыдущую позицию.
Когда текущее отклонение ниже нижней линии на графике отклонений  - входим в позицию рассчитывая на схождение инструментов. Закрываем предыдущую позицию.

*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCointegrationSideTrader")]
    public class PairCointegrationSideTrader : BotPanel
    {

        public PairCointegrationSideTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            _pairTrader.CointegrationPositionSideChangeEvent += _pairTrader_CointegrationPositionSideChangeEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            MaxPositionsCount = CreateParameter("max poses count", 5, 5, 5, 5);

            Description = "The robot trades on a chart of deviations of one instrument from another, " +
                "calculated through their difference with the multiplier. " +
                "Two lines, calculated from the standard deviation multiplied by the multiplier, " +
                "are superimposed on this graph (Cointegration). Above and below zero. " +
                "When the current deviation is higher than the upper line on the deviation chart - " +
                "we enter a position expecting the instruments to converge. We close the previous position. " +
                "When the current deviation is below the bottom line on the Deviation chart - enter the position," +
                " counting on the instruments convergence. Closing the previous position.";
        }

        BotTabPair _pairTrader;

        public override string GetNameStrategyType()
        {
            return "PairCointegrationSideTrader";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private StrategyParameterInt MaxPositionsCount;

        public StrategyParameterString Regime;

        private void _pairTrader_CointegrationPositionSideChangeEvent(CointegrationLineSide side, PairToTrade pair)
        {
            if(Regime.ValueString == "Off")
            {
                return;
            }

            if(pair.HavePositions)
            {
                ClosePositionLogic(pair);
                OpenPositionLogic(pair);
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
            if(_pairTrader.PairsWithPositionsCount >= MaxPositionsCount.ValueInt)
            {
                return;
            }

            if(pair.SideCointegrationValue == pair.LastEntryCointegrationSide)
            {
                return;
            }

            if(pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.SellSec1BuySec2();
            }
            else if(pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.BuySec1SellSec2();
            }
        }

    }
}