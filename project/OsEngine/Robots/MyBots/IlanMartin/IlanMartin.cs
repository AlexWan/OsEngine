using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;

namespace OsEngine.Robots.MyBots.IlanMartin
{
    [Bot("IlanMartin")]

    internal class IlanMartin : BotPanel
    {

        public IlanMartin(string name, StartProgram startProgram) : base(name, startProgram)
        {







        }

        #region Fields ==============================




        #endregion



        #region Methods ==============================


        public override string GetNameStrategyType()
        {
            return nameof(IlanMartin);
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
        #endregion




    }
}
