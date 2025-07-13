/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
Tech sample for OsEngine:
Example showing blocking of indicators for calculating.
Useful when optimizing robots that do not need all created indicators.
*/

namespace OsEngine.Robots.TechSamples
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    [Bot("BlockIndicatorsSample")]
    public class BlockIndicatorsSample : BotPanel
    {
        // Reference to the main traiding tab
        private BotTabSimple _tab;

        // Indicators
        private Aindicator _bollinger;
        private Aindicator _sma;
        private Aindicator _atr;

        // Indicators Settings
        private StrategyParameterBool _bollingerIsOn;
        private StrategyParameterBool _smaIsOn;
        private StrategyParameterBool _atrIsOn;

        public BlockIndicatorsSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Create indicator bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");

            // Create indicator sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");

            // Create indicator atr
            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "atr", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "AtrArea");

            // Indicator settings
            _bollingerIsOn = CreateParameter("Bollinger is ON", true);
            _smaIsOn = CreateParameter("Sma is ON", true);
            _atrIsOn = CreateParameter("Atr is ON", true);

            _stopOrActivateIndicators();
            
            // Subscribe to the indicator update event
            ParametrsChangeByUser += _blockIndicatorsSample_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel98;
        }

        private void _blockIndicatorsSample_ParametrsChangeByUser()
        {
            _stopOrActivateIndicators();
        }

        private void _stopOrActivateIndicators()
        {
            if(_bollingerIsOn.ValueBool 
                != _bollinger.IsOn)
            {
                _bollinger.IsOn = _bollingerIsOn.ValueBool;
                _bollinger.Reload();
            }

            if (_smaIsOn.ValueBool
               != _sma.IsOn)
            {
                _sma.IsOn = _smaIsOn.ValueBool;
                _sma.Reload();
            }

            if (_atrIsOn.ValueBool
                != _atr.IsOn)
            {
                _atr.IsOn = _atrIsOn.ValueBool;
                _atr.Reload();
            }
        }

        public override string GetNameStrategyType()
        {
            return "BlockIndicatorsSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
          
        }
    }
}