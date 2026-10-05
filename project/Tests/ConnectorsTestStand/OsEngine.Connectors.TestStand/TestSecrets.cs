/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;

namespace OsEngine.Connectors.TestStand
{
    /// <summary>
    /// Stores the connector access token for the test stand.
    /// Loaded from a local token file next to the executable (by default
    /// tinvest-token.txt, configurable via test-stand-config.json / --token-file):
    /// a single line with the token. The file must NOT be committed
    /// to the repository. When the file is missing, modules are marked
    /// as SKIPPED instead of failing.
    /// </summary>
    public class TestSecrets
    {
        public string Token { get; set; } = string.Empty;

        public bool HasToken => !string.IsNullOrWhiteSpace(Token);

        /// <summary>
        /// Load the connector token from the token file next to the executable.
        /// Returns an empty instance when the file is missing or empty.
        /// </summary>
        public static TestSecrets Load(string baseDirectory, string tokenFileName)
        {
            string filePath = Path.Combine(baseDirectory, tokenFileName);

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[Secrets] {tokenFileName} not found next to the executable. Modules will be SKIPPED.");
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
                    Console.WriteLine($"[Secrets] {tokenFileName} is empty. Modules will be SKIPPED.");
                    return new TestSecrets();
                }

                Console.WriteLine($"[Secrets] Connector token loaded from {filePath}");

                return new TestSecrets
                {
                    Token = token
                };
            }
            catch (Exception error)
            {
                Console.WriteLine($"[Secrets] Failed to load {filePath}: {error.Message}. Modules will be SKIPPED.");
                return new TestSecrets();
            }
        }
    }
}
