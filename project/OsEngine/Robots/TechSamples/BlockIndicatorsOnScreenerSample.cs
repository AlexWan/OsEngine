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
Tech sample for OsEngine:
Demonstrates switching Bollinger, SMA, and ATR indicators on/off in a Screener tab.
*/

namespace OsEngine.Robots.TechSamples
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    [Bot("BlockIndicatorsOnScreenerSample")]
    public class BlockIndicatorsOnScreenerSample : BotPanel
    {
        // Reference to the main screener tab
        private BotTabScreener _screenerSource;
        
        // Indicators Settings
        private StrategyParameterBool _bollingerIsOn;
        private StrategyParameterBool _smaIsOn;
        private StrategyParameterBool _atrIsOn;

        public BlockIndicatorsOnScreenerSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main screener tab
            TabCreate(BotTabType.Screener);
            _screenerSource = TabsScreener[0];

            // Create indicator bollinger
            _screenerSource.CreateCandleIndicator(1, "Bollinger", null, "Prime");

            // Create indicator sma
            _screenerSource.CreateCandleIndicator(2, "Sma", null, "Prime");

            // Create indicator atr
            _screenerSource.CreateCandleIndicator(3, "ATR", null, "Second");

            // Indicator Settings
            _bollingerIsOn = CreateParameter("Bollinger is ON", true);
            _smaIsOn = CreateParameter("Sma is ON", true);
            _atrIsOn = CreateParameter("Atr is ON", true);

            // Subscribe to the candle finished event
            _screenerSource.CandleFinishedEvent += _screenerSource_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel97;
        }

        private void _screenerSource_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // Create indicator bollinger
            Aindicator bollinger = (Aindicator)tab.Indicators[0];

            // Create indicator sma
            Aindicator sma = (Aindicator)tab.Indicators[1];

            // Create indicator atr
            Aindicator atr = (Aindicator)tab.Indicators[2];

            if (_bollingerIsOn.ValueBool
               != bollinger.IsOn)
            {
                bollinger.IsOn = _bollingerIsOn.ValueBool;
                bollinger.Reload();
            }

            if (_smaIsOn.ValueBool
               != sma.IsOn)
            {
                sma.IsOn = _smaIsOn.ValueBool;
                sma.Reload();
            }

            if (_atrIsOn.ValueBool
                != atr.IsOn)
            {
                atr.IsOn = _atrIsOn.ValueBool;
                atr.Reload();
            }
        }

        public override string GetNameStrategyType()
        {
            return "BlockIndicatorsOnScreenerSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}