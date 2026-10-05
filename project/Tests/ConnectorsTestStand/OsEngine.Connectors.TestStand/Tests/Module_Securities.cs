/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Linq;

namespace OsEngine.Connectors.TestStand.Tests
{
    /// <summary>
    /// Module Securities: V1 - validation of security fields.
    /// </summary>
    public class Module_Securities : ConnectorsTestsBase
    {
        protected override string ModuleName => KnownTests.ModuleSecurities;

        protected override ServerTestInfo[] ModuleTests =>
            KnownTests.All.Where(t => t.Module == KnownTests.ModuleSecurities).ToArray();

        protected override bool ModuleRequiresLiveTrade => false;

        public Module_Securities(TestContext context, string testFilter) : base(context, testFilter)
        {
        }
    }
}
