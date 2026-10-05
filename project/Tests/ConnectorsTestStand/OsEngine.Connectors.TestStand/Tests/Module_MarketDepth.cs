/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module MarketDepth: V2 - market depth streaming on several securities.
    /// </summary>
    public class Module_MarketDepth : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleMarketDepth;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleMarketDepth).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_MarketDepth(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
