/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module BotTabOrders: B1-B6 - BotTabSimple server stop order methods
    /// (open/close via stop-limit and stop-market, cancel, auto-rest-cancel).
    /// Runs only with --live-trade. Requires trading hours (live ticks).
    /// </summary>
    public class Module_BotTabOrders : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleBotTabOrders;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleBotTabOrders).ToArray();

        protected override bool ModuleRequiresLiveTrade => true;

        public Module_BotTabOrders(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
