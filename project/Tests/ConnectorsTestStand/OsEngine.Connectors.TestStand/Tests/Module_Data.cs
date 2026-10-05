/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Data: D1 - history integrity, D2 - candles validation,
    /// D3 - ticks validation, D4 - stress candles, D5 - stress trades.
    /// </summary>
    public class Module_Data : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleData;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleData).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_Data(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
