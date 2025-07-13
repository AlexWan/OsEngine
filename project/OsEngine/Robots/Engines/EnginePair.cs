﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Engines
{
    [Bot("EnginePair")] // We create an attribute so that we don't write anything to the BotFactory
    public class EnginePair : BotPanel
    {
        public EnginePair(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Pair);

            Description = OsLocalization.Description.DescriptionLabel30;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "EnginePair";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
           
        }
    }
}