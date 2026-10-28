/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;

namespace OsEngine.Robots.HhfIndicators
{
    /// <summary>
    /// Сэмпл: создание стаканного индикатора на обычном табе (BotTabSimple)
    /// через BotTabSimple.CreateMarketDepthIndicator с привязкой параметров к StrategyParameter.
    /// </summary>
    [Bot("HhfSpreadBot")]
    public class HhfSpreadBot : BotPanel
    {
        private BotTabSimple _tab;

        private AMarketDepth _spread;

        public StrategyParameterString Regime;

        public StrategyParameterString Mode;

        public StrategyParameterInt Period;

        public HhfSpreadBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            Mode = CreateParameter("Mode", "Absolute", new[] { "Absolute", "Percent" });
            Period = CreateParameter("Period", 10, 1, 60, 1);

            _spread = _tab.CreateMarketDepthIndicator(this, "Spread", false);

            if (_spread != null)
            {
                ((ParameterMarketDepthString)_spread.Parameters.Find(p => p.Name == "Mode")).ValueString = Mode.ValueString;
                ((ParameterMarketDepthInt)_spread.Parameters.Find(p => p.Name == "Period")).ValueInt = Period.ValueInt;
            }

            ParametrsChangeByUser += HhfSpreadBot_ParametrsChangeByUser;

            Description = "HhfSpreadBot: sample of creating a market depth indicator (Spread) on BotTabSimple via CreateMarketDepthIndicator.";
        }

        private void HhfSpreadBot_ParametrsChangeByUser()
        {
            if (_spread == null)
            {
                return;
            }

            ParameterMarketDepthString mode = (ParameterMarketDepthString)_spread.Parameters.Find(p => p.Name == "Mode");
            ParameterMarketDepthInt period = (ParameterMarketDepthInt)_spread.Parameters.Find(p => p.Name == "Period");

            bool changed = false;

            if (mode.ValueString != Mode.ValueString)
            {
                mode.ValueString = Mode.ValueString;
                changed = true;
            }

            if (period.ValueInt != Period.ValueInt)
            {
                period.ValueInt = Period.ValueInt;
                changed = true;
            }

            if (changed)
            {
                _spread.Recalculate();
                _spread.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "HhfSpreadBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
