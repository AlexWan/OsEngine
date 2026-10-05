/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Trades: V3 - trades streaming on several securities.
    /// </summary>
    public class Module_Trades : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleTrades;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleTrades).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_Trades(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
