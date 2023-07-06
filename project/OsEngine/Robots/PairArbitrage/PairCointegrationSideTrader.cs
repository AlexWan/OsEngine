/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

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