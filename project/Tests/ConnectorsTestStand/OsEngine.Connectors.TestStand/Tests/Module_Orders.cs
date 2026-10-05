/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Orders: O1-O16 - real orders on the account (fake orders,
    /// limits, market, cancels, price change, spam, reconnect, stop orders).
    /// Runs only with --live-trade. Requires trading hours (live ticks).
    /// </summary>
    public class Module_Orders : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleOrders;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleOrders).ToArray();

        protected override bool ModuleRequiresLiveTrade => true;

        public Module_Orders(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
