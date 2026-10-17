/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Общие данные для тестов риск-паритета: фикстуры в папке TestData рядом с exe стенда.
    /// </summary>
    internal static class CapitalAllocationTestData
    {
        public static readonly string Directory =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");

        public static string[] AllFilePaths()
        {
            return System.IO.Directory.GetFiles(Directory, "*.txt");
        }

        public static HashSet<string> AllBotNames()
        {
            return new HashSet<string>(AllFilePaths().Select(f => Path.GetFileName(f).Split('_')[0]));
        }

        public static int BotCount()
        {
            return AllFilePaths().Length;
        }
    }
}
