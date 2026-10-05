/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Portfolio: P1 - portfolio / positions / asset validation.
    /// </summary>
    public class Module_Portfolio : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModulePortfolio;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModulePortfolio).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_Portfolio(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
