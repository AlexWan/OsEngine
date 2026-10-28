/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.HhfIndicators
{
    /// <summary>
    /// Сэмпл: создание стаканных индикаторов на скринере (BotTabScreener)
    /// через BotTabScreener.CreateMarketDepthIndicator. Индикатор создаётся на каждом табе (каждой бумаге).
    /// </summary>
    [Bot("HhfSpreadScreener")]
    public class HhfSpreadScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        public StrategyParameterString Mode;

        public StrategyParameterInt Period;

        public HhfSpreadScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            Mode = CreateParameter("Mode", "Absolute", new[] { "Absolute", "Percent" });
            Period = CreateParameter("Period", 10, 1, 60, 1);

            _screenerTab.CreateMarketDepthIndicator(1, "Spread",
                new List<string> { Mode.ValueString, Period.ValueInt.ToString() }, false);

            Description = "HhfSpreadScreener: sample of creating a market depth indicator (Spread) on BotTabScreener via CreateMarketDepthIndicator.";
        }

        public override string GetNameStrategyType()
        {
            return "HhfSpreadScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
