/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json;
using WikiConnectionTest.Models;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Loads and saves connector credentials.
    /// The file is ignored by git and must never be committed.
    /// </summary>
    public static class SecretsService
    {
        private const string FileName = "connection-secrets.json";

        public static ConnectionSecrets Load(string baseDirectory)
        {
            string filePath = Path.Combine(baseDirectory, FileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    ConnectionSecrets? secrets = JsonSerializer.Deserialize<ConnectionSecrets>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (secrets != null)
                    {
                        Console.WriteLine($"[Secrets] Loaded from {filePath}");

                        if (ConsoleHelper.IsRunByUser())
                        {
                            bool added = PromptMissing(secrets);

                            if (added)
                            {
                                Save(filePath, secrets);
                                Console.WriteLine($"[Secrets] Saved to {filePath}");
                                Console.WriteLine("[Secrets] This file is ignored by git. Do not commit it.");
                            }
                        }

                        return secrets;
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine($"[Secrets] Failed to load {filePath}: {error.Message}");
                }
            }

            if (ConsoleHelper.IsRunByUser())
            {
                Console.WriteLine("[Secrets] Connector credentials not found. Enter tokens for future connectors or press Enter to skip.");

                ConnectionSecrets newSecrets = Prompt();
                Save(filePath, newSecrets);

                Console.WriteLine($"[Secrets] Saved to {filePath}");
                Console.WriteLine("[Secrets] This file is ignored by git. Do not commit it.");

                return newSecrets;
            }

            Console.WriteLine("[Secrets] Connector credentials not found. Running in non-interactive mode, creating empty secrets file.");

            var emptySecrets = new ConnectionSecrets();
            Save(filePath, emptySecrets);
            return emptySecrets;
        }

        public static void Save(string filePath, ConnectionSecrets secrets)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(filePath, json);
        }

        private static ConnectionSecrets Prompt()
        {
            var secrets = new ConnectionSecrets();
            PromptMissing(secrets);
            return secrets;
        }

        private static bool PromptMissing(ConnectionSecrets secrets)
        {
            bool added = false;

            if (!secrets.Connectors.ContainsKey("TInvest"))
            {
                Console.Write("TInvest token (Enter to skip): ");
                string? tinvest = Console.ReadLine()?.Trim();

                if (!string.IsNullOrEmpty(tinvest))
                {
                    secrets.Connectors["TInvest"] = new Dictionary<string, string>
                    {
                        ["Token"] = tinvest
                    };
                    added = true;
                }
            }

            var alorParameters = new Dictionary<string, string>
            {
                ["Token"] = "Alor token",
                ["Portfolio Spot"] = "Alor portfolio spot",
                ["Portfolio FORTS"] = "Alor portfolio FORTS",
                ["Portfolio currency"] = "Alor portfolio currency",
                ["Portfolio spare"] = "Alor portfolio spare"
            };

            if (!secrets.Connectors.TryGetValue("Alor", out Dictionary<string, string>? alorSecrets)
                || alorSecrets == null)
            {
                alorSecrets = new Dictionary<string, string>();
            }

            foreach (KeyValuePair<string, string> parameter in alorParameters)
            {
                if (!alorSecrets.ContainsKey(parameter.Key)
                    || string.IsNullOrEmpty(alorSecrets[parameter.Key]))
                {
                    Console.Write($"{parameter.Value} (Enter to skip): ");
                    string? value = Console.ReadLine()?.Trim();

                    if (!string.IsNullOrEmpty(value))
                    {
                        alorSecrets[parameter.Key] = value;
                        added = true;
                    }
                }
            }

            if (alorSecrets.Count > 0)
            {
                secrets.Connectors["Alor"] = alorSecrets;
            }

            return added;
        }
    }
}
