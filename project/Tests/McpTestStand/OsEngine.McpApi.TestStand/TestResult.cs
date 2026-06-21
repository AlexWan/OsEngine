/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Result of a single test.
    /// </summary>
    public class TestResult
    {
        public string Name { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public static TestResult Passed(string name, string? message = null)
        {
            return new TestResult { Name = name, Success = true, Message = message ?? string.Empty };
        }

        public static TestResult Failed(string name, string message)
        {
            return new TestResult { Name = name, Success = false, Message = message };
        }
    }
}
