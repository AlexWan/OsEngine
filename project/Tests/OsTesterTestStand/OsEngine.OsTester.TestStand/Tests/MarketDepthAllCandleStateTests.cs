/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Модуль 7 — Тип данных MarketDepthAllCandleState: прогон на свечах, собранных из стаканов.
    /// </summary>
    public class MarketDepthAllCandleStateTests : DataTypeTestsBase
    {
        public MarketDepthAllCandleStateTests(TestContext context)
            : base(context, "MarketDepthAllCandleState", "TesterDepth", "MarketDepthAllCandleState", false)
        {
        }
    }
}
