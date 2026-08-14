/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;

namespace StopOrdersTestStand
{
    /// <summary>
    /// Stores the T-Invest token for the test stand.
    /// Loaded from a local tinvest-token.txt next to the executable:
    /// a single line with the token. The file must NOT be committed
    /// to the repository. When the file is missing, trading modules
    /// are marked as SKIPPED instead of failing.
    /// </summary>
    public class TestSecrets
    {
        public string TInvestToken { get; set; } = string.Empty;

        public bool HasTInvestToken => !string.IsNullOrWhiteSpace(TInvestToken);

        private const string FileName = "tinvest-token.txt";

        /// <summary>
        /// Load the T-Invest token from tinvest-token.txt next to the executable.
        /// Returns an empty instance when the file is missing or empty.
        /// </summary>
        public static TestSecrets Load(string baseDirectory)
        {
            string filePath = Path.Combine(baseDirectory, FileName);

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[Secrets] {FileName} not found next to the executable. Trading modules will be SKIPPED.");
                    return new TestSecrets();
                }

                string token = string.Empty;

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string? line = reader.ReadLine();

                    if (line != null)
                    {
                        token = line.Trim();
                    }
                }

                if (token.Length == 0)
                {
                    Console.WriteLine($"[Secrets] {FileName} is empty. Trading modules will be SKIPPED.");
                    return new TestSecrets();
                }

                Console.WriteLine($"[Secrets] T-Invest token loaded from {filePath}");

                return new TestSecrets
                {
                    TInvestToken = token
                };
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Secrets] Failed to load {filePath}: {error.Message}. Trading modules will be SKIPPED.");
                return new TestSecrets();
            }
        }
    }
}
