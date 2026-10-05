/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Connection: C1 - connect/disconnect status cycles,
    /// C2 - subscribe all securities of a class, C3 - memory stress,
    /// C4 - real-time candles validation, C5 - screener.
    /// </summary>
    public class Module_Connection : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleConnection;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleConnection).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_Connection(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
