/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.OsTester.TestStand;

namespace OsEngine.OsTester.TestStand.Tests
{
    /// <summary>
    /// Модуль 6 — Тип данных TickAllCandleState: прогон на свечах, собранных из тиков.
    /// </summary>
    public class TickAllCandleStateTests : DataTypeTestsBase
    {
        public TickAllCandleStateTests(TestContext context)
            : base(context, "TickAllCandleState", "TesterTicks", "TickAllCandleState", true)
        {
        }
    }
}
