/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Collections.Generic;
using OsEngine.Language;


/* Description
TechSample robot for OsEngine

An example of drawing a series of indicator data calculated in the robot.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomDataInIndicatorSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class CustomDataInIndicatorSample : BotPanel
    {
        // Simple tab
        private BotTabSimple _tab;

        // Indicator
        private Aindicator _indicatorEmpty;

        public CustomDataInIndicatorSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create Simple tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Create indicator EmptyIndicator
            _indicatorEmpty = IndicatorsFactory.CreateIndicatorByName("EmptyIndicator", name + "EmptyIndicator", false);
            _indicatorEmpty = (Aindicator)_tab.CreateCandleIndicator(_indicatorEmpty, "SecondArea");

            Description = OsLocalization.Description.DescriptionLabel102;
        }

        // Candle finished event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            decimal dataPoint = candles[candles.Count - 1].Close / 2;

            _indicatorEmpty.DataSeries[0].Values[_indicatorEmpty.DataSeries[0].Values.Count-1] = dataPoint;
            _indicatorEmpty.RePaint();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CustomDataInIndicatorSample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}