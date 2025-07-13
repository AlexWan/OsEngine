/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*
Description

Trading robot for osengine
if the correlation is higher than 0.9 and we are on some
side of the cointegration - enter, counting on the pair convergence

Exit by the inverse cointegration signal
*/

namespace OsEngine.Robots.PairArbitrage
{
    [Bot("PairCorrelationTrader")]
    public class PairCorrelationTrader : BotPanel
    {
        BotTabPair _pairTrader;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositionsCount;
        
        public PairCorrelationTrader(string name, StartProgram startProgram)
           : base(name, startProgram)
        {
            TabCreate(BotTabType.Pair);
            _pairTrader = this.TabsPair[0];

            // Subscribe to the cointegration change event
            _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _maxPositionsCount = CreateParameter("max poses count", 5, 5, 5, 5);

            Description = OsLocalization.Description.DescriptionLabel71;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PairCorrelationTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _pairTrader_CorrelationChangeEvent(System.Collections.Generic.List<PairIndicatorValue> correlation, PairToTrade pair)
        {
            if (_regime.ValueString == "Off")
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

        // Position close logic
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

        // Position open logic
        private void OpenPositionLogic(PairToTrade pair)
        {
            if(pair.CorrelationLast < 0.9m)
            {
                return;
            }

            if (_pairTrader.PairsWithPositionsCount >= _maxPositionsCount.ValueInt)
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